using Messaging.Processing;

namespace Messaging.UnitTests;

public class StuckEventDetectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly StuckEventDetector _sut = new(TimeSpan.FromMinutes(5), maxAttempts: 5);

    private static EventState State(EventStatus status, TimeSpan? age, int attempts) =>
        new(Guid.NewGuid(), status, age is null ? null : Now - age.Value, attempts);

    [Theory]
    [InlineData(EventStatus.Received)]
    [InlineData(EventStatus.Done)]
    [InlineData(EventStatus.Failed)]
    public void Dado_evento_fora_de_processing_Quando_decidir_Entao_nada(EventStatus status)
    {
        _sut.Decide(State(status, TimeSpan.FromHours(1), 1), Now).Should().Be(RecoveryAction.None);
    }

    [Fact]
    public void Dado_processing_dentro_do_timeout_Quando_decidir_Entao_nada()
    {
        _sut.Decide(State(EventStatus.Processing, TimeSpan.FromMinutes(1), 1), Now).Should().Be(RecoveryAction.None);
    }

    [Fact]
    public void Dado_processing_alem_do_timeout_Quando_decidir_Entao_recoloca_na_fila()
    {
        _sut.Decide(State(EventStatus.Processing, TimeSpan.FromMinutes(6), 1), Now).Should().Be(RecoveryAction.Requeue);
    }

    [Fact]
    public void Dado_processing_preso_com_tentativas_esgotadas_Quando_decidir_Entao_vai_para_dlq()
    {
        _sut.Decide(State(EventStatus.Processing, TimeSpan.FromMinutes(6), 5), Now).Should().Be(RecoveryAction.MoveToDlq);
    }

    [Fact]
    public void Dado_processing_sem_data_de_inicio_Quando_decidir_Entao_trata_como_preso()
    {
        _sut.Decide(State(EventStatus.Processing, null, 1), Now).Should().Be(RecoveryAction.Requeue);
    }

    [Fact]
    public void Dado_timeout_invalido_Quando_criar_Entao_lanca()
    {
        var act = () => new StuckEventDetector(TimeSpan.Zero, 3);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
