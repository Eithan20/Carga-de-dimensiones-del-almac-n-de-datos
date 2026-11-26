namespace OpinionesETL.Application;

/// <summary>Opciones de la carga de dimensiones (sección "DimensionLoad" de appsettings.json).</summary>
public sealed class DimensionLoadOptions
{
    public const string SectionName = "DimensionLoad";

    /// <summary>false = el servicio solo extrae a staging y no carga las dimensiones.</summary>
    public bool Enabled { get; set; } = true;
}
