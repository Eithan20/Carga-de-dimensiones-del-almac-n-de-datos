namespace OpinionesETL.Domain.Entities;

/// <summary>
/// Opinión tal como llega de cualquier fuente (encuesta, reseña web o comentario social).
/// Todos los campos son texto: en la extracción no se transforma nada, eso se hace después.
/// </summary>
public sealed record RawOpinion(
    string Canal,            // Encuesta | ResenaWeb | ComentarioSocial
    string Origen,           // archivo, tabla o endpoint de donde salió
    string? IdOrigen,
    string? IdCliente,
    string? IdProducto,
    string? Fecha,
    string? Comentario,
    string? Clasificacion,
    string? Puntaje,
    string? Plataforma);
