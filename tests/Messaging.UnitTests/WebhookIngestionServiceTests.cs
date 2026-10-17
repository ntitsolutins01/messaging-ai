using System.Text;
using Messaging.Ingestion;
using Messaging.TestSupport;

namespace Messaging.UnitTests;

public class WebhookIngestionServiceTests
{
    private const string Secret = "test-secret";
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(Now);
    private readonly InMemoryRawEventStore _store = new();
    private readonly WebhookIngestionService _sut;

    public WebhookIngestionServiceTests()
    {
        var validator = new WebhookSignatureValidator(Secret, _clock, TimeSpan.FromMinutes(5));
        _sut = new WebhookIngestionService(validator, _store, _clock);
    }

    private static byte[] BodyOf(string json) => Encoding.UTF8.GetBytes(json);

    private Task<IngestionOutcome> Ingest(string? eventId, byte[] body, string? signature = null) =>
        _sut.IngestAsync("acme", eventId, body, signature ?? WebhookSigner.Sign(Secret, Now, body), CancellationToken.None);

    [Fact]
    public async Task Dado_assinatura_invalida_Quando_ingerir_Entao_rejeita_e_nao_grava()
    {
        var outcome = await Ingest("evt_1", BodyOf("{}"), signature: "t=1,v1=00");

        outcome.Should().Be(IngestionOutcome.InvalidSignature);
        _store.EventCount.Should().Be(0);
        _store.OutboxCount.Should().Be(0);
    }

    [Fact]
    public async Task Dado_evento_novo_Quando_ingerir_Entao_grava_evento_bruto_e_outbox()
    {
        var outcome = await Ingest("evt_1", BodyOf("{\"a\":1}"));

        outcome.Should().Be(IngestionOutcome.Accepted);
        _store.EventCount.Should().Be(1);
        _store.OutboxCount.Should().Be(1);
    }

    [Fact]
    public async Task Dado_mesmo_evento_repetido_Quando_ingerir_Entao_duplicata_sem_novo_efeito()
    {
        var body = BodyOf("{\"a\":1}");
        await Ingest("evt_1", body);

        var second = await Ingest("evt_1", body);

        second.Should().Be(IngestionOutcome.Duplicate);
        _store.EventCount.Should().Be(1);
        _store.OutboxCount.Should().Be(1);
    }

    [Fact]
    public async Task Dado_provedor_sem_id_de_evento_Quando_repetir_o_mesmo_corpo_Entao_duplicata_por_hash()
    {
        var body = BodyOf("{\"a\":1}");
        await Ingest(null, body);

        var second = await Ingest(null, body);

        second.Should().Be(IngestionOutcome.Duplicate);
        _store.EventCount.Should().Be(1);
    }

    [Fact]
    public async Task Dado_vinte_envios_concorrentes_do_mesmo_evento_Quando_ingerir_Entao_um_unico_efeito()
    {
        var body = BodyOf("{\"a\":1}");

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Ingest("evt_1", body)));

        outcomes.Count(o => o == IngestionOutcome.Accepted).Should().Be(1);
        outcomes.Count(o => o == IngestionOutcome.Duplicate).Should().Be(19);
        _store.EventCount.Should().Be(1);
        _store.OutboxCount.Should().Be(1);
    }

    [Fact]
    public async Task Dado_corpo_que_nao_e_json_valido_Quando_ingerir_Entao_grava_o_bruto_mesmo_assim()
    {
        var outcome = await Ingest("evt_9", BodyOf("isto nao e json"));

        outcome.Should().Be(IngestionOutcome.Accepted);
        _store.EventCount.Should().Be(1);
    }
}
