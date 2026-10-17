using System.Net;
using System.Text;
using Messaging.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Messaging.IntegrationTests;

/// <summary>
/// Testes da API de ponta a ponta com o store em memória.
/// Os testes com PostgreSQL/Kafka/RabbitMQ reais (Testcontainers) entram com as specs 002 e 004.
/// </summary>
public class WebhookEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Secret = "integration-secret";

    private readonly WebApplicationFactory<Program> _factory;

    static WebhookEndpointTests()
    {
        // Variável de ambiente é lida pelo WebApplication.CreateBuilder antes de qualquer UseSetting.
        Environment.SetEnvironmentVariable("Webhook__Secret", Secret);
    }

    public WebhookEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static HttpRequestMessage Request(string eventId, string json, string? signature = null)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var message = new HttpRequestMessage(HttpMethod.Post, "/webhooks/acme")
        {
            Content = new ByteArrayContent(body),
        };
        message.Headers.Add("X-Event-Id", eventId);
        message.Headers.Add("X-Signature", signature ?? WebhookSigner.Sign(Secret, DateTimeOffset.UtcNow, body));
        return message;
    }

    [Fact]
    public async Task Dado_webhook_valido_Quando_postar_Entao_200()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(Request("evt_a", "{\"a\":1}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Dado_assinatura_invalida_Quando_postar_Entao_401()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(Request("evt_b", "{\"a\":1}", signature: "t=1,v1=00"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dado_mesmo_webhook_repetido_em_paralelo_Quando_postar_Entao_todos_200()
    {
        var client = _factory.CreateClient();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => client.SendAsync(Request("evt_c", "{\"a\":1}"))));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task Dado_provedor_com_nome_invalido_Quando_postar_Entao_400()
    {
        var client = _factory.CreateClient();
        var message = new HttpRequestMessage(HttpMethod.Post, "/webhooks/..%2F..%2Fadmin")
        {
            Content = new ByteArrayContent(Array.Empty<byte>()),
        };

        var response = await client.SendAsync(message);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dado_a_api_no_ar_Quando_consultar_health_Entao_200_e_correlation_id()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("X-Correlation-Id").Should().BeTrue();
    }
}
