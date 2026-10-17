using Messaging.Contracts.Processing;
using Messaging.Operations;

namespace Messaging.UnitTests;

public class ReprocessEventHandlerTests
{
    private sealed class FakeReprocessor : IEventReprocessor
    {
        public List<Guid> Calls { get; } = new();

        public Task<ReprocessOutcome> ReprocessAsync(Guid eventId, CancellationToken cancellationToken)
        {
            Calls.Add(eventId);
            return Task.FromResult(ReprocessOutcome.Requeued);
        }
    }

    [Fact]
    public async Task Dado_evento_valido_Quando_reprocessar_Entao_delega_ao_modulo_processing()
    {
        var reprocessor = new FakeReprocessor();
        var id = Guid.NewGuid();

        var outcome = await new ReprocessEventHandler(reprocessor).HandleAsync(id, CancellationToken.None);

        outcome.Should().Be(ReprocessOutcome.Requeued);
        reprocessor.Calls.Should().ContainSingle().Which.Should().Be(id);
    }

    [Fact]
    public async Task Dado_id_vazio_Quando_reprocessar_Entao_lanca_sem_chamar_processing()
    {
        var reprocessor = new FakeReprocessor();

        var act = () => new ReprocessEventHandler(reprocessor).HandleAsync(Guid.Empty, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        reprocessor.Calls.Should().BeEmpty();
    }
}
