using OpinionesETL.Domain.Entities;

namespace OpinionesETL.Domain.Interfaces;

/// <summary>Carga de dimensiones en el almacén de datos mediante procedimientos almacenados.</summary>
public interface IDimensionLoader
{
    /// <summary>Ejecuta el procedimiento almacenado que carga la dimensión indicada (ej. "DimProducto").</summary>
    Task<DimensionLoadResult> LoadAsync(string dimension, CancellationToken cancellationToken = default);
}
