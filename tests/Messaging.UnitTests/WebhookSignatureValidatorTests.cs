using System.Text;
using Messaging.Ingestion;
using Messaging.TestSupport;

namespace Messaging.UnitTests;

public class WebhookSignatureValidatorTests
{
    private const string Secret = "test-secret";
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("{\"id\":\"evt_1\"}");

    private readonly FakeClock _clock = new(Now);

    private WebhookSignatureValidator CreateSut(string secret = Secret) =>
        new(secret, _clock, TimeSpan.FromMinutes(5));

    [Fact]
    public void Dado_assinatura_correta_Quando_validar_Entao_valida()
    {
        var header = WebhookSigner.Sign(Secret, Now, Body);

        CreateSut().Validate(Body, header).Should().Be(SignatureResult.Valid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Dado_cabecalho_ausente_Quando_validar_Entao_missing(string? header)
    {
        CreateSut().Validate(Body, header).Should().Be(SignatureResult.Missing);
    }

    [Theory]
    [InlineData("lixo")]
    [InlineData("t=abc,v1=00")]
    [InlineData("t=1760000000")]
    [InlineData("v1=00ff")]
    [InlineData("t=1760000000,v1=nao-e-hex")]
    [InlineData("t=99999999999999999999,v1=00ff")]
    public void Dado_cabecalho_malformado_Quando_validar_Entao_malformed(string header)
    {
        CreateSut().Validate(Body, header).Should().Be(SignatureResult.Malformed);
    }

    [Fact]
    public void Dado_corpo_adulterado_Quando_validar_Entao_mismatch()
    {
        var header = WebhookSigner.Sign(Secret, Now, Body);
        var tampered = Encoding.UTF8.GetBytes("{\"id\":\"evt_2\"}");

        CreateSut().Validate(tampered, header).Should().Be(SignatureResult.Mismatch);
    }

    [Fact]
    public void Dado_segredo_diferente_Quando_validar_Entao_mismatch()
    {
        var header = WebhookSigner.Sign("outro-segredo", Now, Body);

        CreateSut().Validate(Body, header).Should().Be(SignatureResult.Mismatch);
    }

    [Fact]
    public void Dado_assinatura_antiga_Quando_validar_Entao_expired()
    {
        var header = WebhookSigner.Sign(Secret, Now.AddMinutes(-6), Body);

        CreateSut().Validate(Body, header).Should().Be(SignatureResult.Expired);
    }

    [Fact]
    public void Dado_assinatura_no_futuro_alem_da_tolerancia_Quando_validar_Entao_expired()
    {
        var header = WebhookSigner.Sign(Secret, Now.AddMinutes(6), Body);

        CreateSut().Validate(Body, header).Should().Be(SignatureResult.Expired);
    }

    [Fact]
    public void Dado_assinatura_dentro_da_tolerancia_Quando_validar_Entao_valida()
    {
        var header = WebhookSigner.Sign(Secret, Now.AddMinutes(-4), Body);

        CreateSut().Validate(Body, header).Should().Be(SignatureResult.Valid);
    }

    [Fact]
    public void Dado_segredo_vazio_Quando_criar_Entao_lanca()
    {
        var act = () => CreateSut(" ");

        act.Should().Throw<ArgumentException>();
    }
}
