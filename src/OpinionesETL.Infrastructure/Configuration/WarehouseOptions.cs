namespace OpinionesETL.Infrastructure.Configuration;

/// <summary>Opciones de acceso al almacén de datos (sección "DimensionLoad").</summary>
public sealed class WarehouseOptions
{
    public const string SectionName = "DimensionLoad";

    public int CommandTimeoutSeconds { get; set; } = 120;
}
