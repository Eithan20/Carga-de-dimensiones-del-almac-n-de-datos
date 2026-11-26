namespace OpinionesETL.Domain.Entities;

/// <summary>Estados posibles de un lote de extracción (una ejecución de una fuente).</summary>
public static class EstadoLote
{
    public const string EnCurso = "EnCurso";
    public const string Completado = "Completado";
    public const string Error = "Error";
    public const string Cancelado = "Cancelado";
}
