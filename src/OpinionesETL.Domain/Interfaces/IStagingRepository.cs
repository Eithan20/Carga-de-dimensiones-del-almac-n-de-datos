using OpinionesETL.Domain.Entities;

namespace OpinionesETL.Domain.Interfaces;

/// <summary>Destino de la extracción: guarda los datos crudos en el área de staging.</summary>
public interface IStagingRepository
{
    /// <summary>Registra el inicio de una extracción y devuelve el id del lote.</summary>
    Task<long> StartBatchAsync(string fuente, string canal, CancellationToken cancellationToken = default);

    /// <summary>Guarda un bloque de opiniones asociado al lote.</summary>
    Task WriteAsync(long loteId, IReadOnlyCollection<RawOpinion> opiniones, CancellationToken cancellationToken = default);

    /// <summary>Cierra el lote con su estado final y sus métricas.</summary>
    Task FinishBatchAsync(long loteId, string estado, long registros, long duracionMs, string? error,
        CancellationToken cancellationToken = default);
}
