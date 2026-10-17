using System.Text.RegularExpressions;
using Messaging.Ingestion;
using Messaging.Observability;
using Messaging.SharedKernel;

const int MaxBodyBytes = 1_048_576;
var providerPattern = new Regex("^[a-z0-9-]{1,50}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

var builder = WebApplication.CreateBuilder(args);

// Segredo nunca no repositório: user-secrets ou variável de ambiente Webhook__Secret.
var secret = builder.Configuration["Webhook:Secret"];
if (string.IsNullOrWhiteSpace(secret))
{
    throw new InvalidOperationException("Webhook:Secret is required. Use user-secrets or the Webhook__Secret environment variable.");
}

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxBodyBytes);

// Traces, métricas e logs via OTLP, só se OTEL_EXPORTER_OTLP_ENDPOINT estiver definido (módulo 13).
builder.AddMessagingObservability("messaging-api");

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IRawEventStore, InMemoryRawEventStore>();
builder.Services.AddSingleton(sp =>
    new WebhookSignatureValidator(secret, sp.GetRequiredService<IClock>(), TimeSpan.FromMinutes(5)));
builder.Services.AddSingleton<WebhookIngestionService>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});

app.MapGet("/health/live", () => Results.Ok());
app.MapGet("/health/ready", () => Results.Ok());

app.MapPost("/webhooks/{provider}", async (
    string provider,
    HttpRequest request,
    WebhookIngestionService ingestion,
    CancellationToken cancellationToken) =>
{
    if (!providerPattern.IsMatch(provider))
    {
        return Results.BadRequest();
    }

    using var buffer = new MemoryStream();
    await request.Body.CopyToAsync(buffer, cancellationToken);

    try
    {
        var outcome = await ingestion.IngestAsync(
            provider,
            request.Headers["X-Event-Id"].FirstOrDefault(),
            buffer.ToArray(),
            request.Headers["X-Signature"].FirstOrDefault(),
            cancellationToken);

        // Duplicata também responde 200: o provedor para de reenviar.
        return outcome == IngestionOutcome.InvalidSignature ? Results.Unauthorized() : Results.Ok();
    }
    catch (ArgumentException)
    {
        return Results.BadRequest();
    }
});

app.Run();

public partial class Program
{
}
