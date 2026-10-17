using System.Diagnostics;

namespace Messaging.SharedKernel;

/// <summary>
/// Fonte de traces do sistema. Usa só System.Diagnostics (sem pacote de OpenTelemetry nos módulos).
/// O host registra a fonte "Messaging" no exporter (ver Messaging.Observability).
/// Regra: nunca coloque payload, segredo ou dado pessoal em tags.
/// </summary>
public static class Telemetry
{
    public const string SourceName = "Messaging";

    public static readonly ActivitySource Source = new(SourceName);
}
