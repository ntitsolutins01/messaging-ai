using System.Reflection;
using Messaging.Contracts.Ingestion;
using Messaging.Ingestion;
using Messaging.Operations;
using Messaging.Processing;
using Messaging.SharedKernel;
using NetArchTest.Rules;

namespace Messaging.ArchitectureTests;

/// <summary>
/// Quality Gate (módulo 09) e Monólito Modular (módulo 15): fronteiras entre módulos são regras executáveis.
/// Módulos só conversam por Messaging.Contracts; nunca por tipos internos do outro módulo.
/// </summary>
public class ModuleBoundaryTests
{
    private const string Ingestion = "Messaging.Ingestion";
    private const string Processing = "Messaging.Processing";
    private const string Operations = "Messaging.Operations";

    private static readonly string[] Brokers = { "Confluent.Kafka", "RabbitMQ.Client" };

    private static readonly Assembly IngestionAssembly = typeof(WebhookIngestionService).Assembly;
    private static readonly Assembly ProcessingAssembly = typeof(StuckEventDetector).Assembly;
    private static readonly Assembly OperationsAssembly = typeof(ReprocessEventHandler).Assembly;
    private static readonly Assembly SharedKernelAssembly = typeof(IdempotencyKey).Assembly;
    private static readonly Assembly ContractsAssembly = typeof(RawEventAccepted).Assembly;

    private static void ShouldNotDependOn(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        result.IsSuccessful.Should().BeTrue(
            "violação de fronteira em: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ingestion_nao_depende_de_outros_modulos() =>
        ShouldNotDependOn(IngestionAssembly, Processing, Operations);

    [Fact]
    public void Processing_nao_depende_de_outros_modulos() =>
        ShouldNotDependOn(ProcessingAssembly, Ingestion, Operations);

    [Fact]
    public void Operations_nao_depende_de_outros_modulos() =>
        ShouldNotDependOn(OperationsAssembly, Ingestion, Processing);

    [Fact]
    public void SharedKernel_nao_depende_de_modulos_nem_de_contratos() =>
        ShouldNotDependOn(SharedKernelAssembly, Ingestion, Processing, Operations, "Messaging.Contracts");

    [Fact]
    public void Contracts_nao_dependem_de_modulos() =>
        ShouldNotDependOn(ContractsAssembly, Ingestion, Processing, Operations);

    [Fact]
    public void Dominio_dos_modulos_nao_conhece_kafka_nem_rabbitmq()
    {
        ShouldNotDependOn(IngestionAssembly, Brokers);
        ShouldNotDependOn(ProcessingAssembly, Brokers);
        ShouldNotDependOn(OperationsAssembly, Brokers);
        ShouldNotDependOn(SharedKernelAssembly, Brokers);
    }
}
