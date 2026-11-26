using OpinionesETL.Domain.Entities;

namespace OpinionesETL.Domain.Interfaces;

/// <summary>
/// Contrato que cumple toda fuente de datos. Para agregar una fuente nueva basta con
/// crear una clase que implemente esta interfaz y registrarla (ver DependencyInjection).
/// </summary>
public interface IExtractor
{
    /// <summary>Nombre corto de la fuente (CSV, BaseDatos, API). Se usa en logs y lotes.</summary>
    string SourceName { get; }

    /// <summary>Canal de las opiniones que entrega (Encuesta, ResenaWeb, ComentarioSocial).</summary>
    string Canal { get; }

    /// <summary>
    /// Entrega las opiniones una por una (streaming), sin cargar la fuente completa en memoria.
    /// </summary>
    IAsyncEnumerable<RawOpinion> ExtractAsync(CancellationToken cancellationToken = default);
}
