namespace OpinionesETL.Application;

/// <summary>Opciones generales de la extracción (sección "Extraction" de appsettings.json).</summary>
public sealed class ExtractionOptions
{
    public const string SectionName = "Extraction";

    /// <summary>Cuántas opiniones se acumulan antes de enviarlas a staging.</summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>Cuántas fuentes se extraen al mismo tiempo.</summary>
    public int MaxDegreeOfParallelism { get; set; } = 3;

    /// <summary>true = hace una sola extracción y termina; false = repite cada IntervalMinutes.</summary>
    public bool RunOnce { get; set; } = true;

    public int IntervalMinutes { get; set; } = 60;
}
