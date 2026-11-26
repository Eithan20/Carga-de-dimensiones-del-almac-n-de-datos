namespace OpinionesETL.Application;

/// <summary>Resultado de extraer una fuente.</summary>
public sealed record SourceResult(
    string Source,
    string Canal,
    string Status,
    long Records,
    TimeSpan Duration,
    string? Error)
{
    public double RecordsPerSecond =>
        Duration.TotalSeconds > 0 ? Records / Duration.TotalSeconds : Records;
}
