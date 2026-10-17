using Messaging.SharedKernel;

namespace Messaging.UnitTests;

public class IdempotencyKeyTests
{
    [Fact]
    public void Dado_provider_e_id_Quando_criar_Entao_normaliza_provider()
    {
        var key = IdempotencyKey.From("  ACME ", " evt_123 ");

        key.Value.Should().Be("acme:evt_123");
    }

    [Fact]
    public void Dado_mesmos_valores_Quando_comparar_Entao_chaves_sao_iguais()
    {
        IdempotencyKey.From("acme", "evt_1").Should().Be(IdempotencyKey.From("ACME", "evt_1"));
    }

    [Theory]
    [InlineData("", "evt")]
    [InlineData("   ", "evt")]
    [InlineData("acme", "")]
    [InlineData("acme", "   ")]
    public void Dado_valor_vazio_Quando_criar_Entao_lanca(string provider, string eventId)
    {
        var act = () => IdempotencyKey.From(provider, eventId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Dado_chave_maior_que_o_limite_Quando_criar_Entao_lanca()
    {
        var act = () => IdempotencyKey.From("acme", new string('x', IdempotencyKey.MaxLength));

        act.Should().Throw<ArgumentException>();
    }
}
