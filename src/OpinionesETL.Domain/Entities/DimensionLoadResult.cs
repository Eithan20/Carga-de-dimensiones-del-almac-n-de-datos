namespace OpinionesETL.Domain.Entities;

/// <summary>Lo que devuelve el procedimiento almacenado de una dimensión.</summary>
public sealed record DimensionLoadResult(string Dimension, int Inserted, int Updated, int Total);
