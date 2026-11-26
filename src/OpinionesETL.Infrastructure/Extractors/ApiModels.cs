namespace OpinionesETL.Infrastructure.Extractors;

/// <summary>Respuesta paginada de la API: { page, pageSize, total, items: [...] }.</summary>
internal sealed record ApiPage(int Page, int PageSize, long Total, List<ApiComment>? Items);

internal sealed record ApiComment(
    string? IdComment,
    string? IdCliente,
    string? IdProducto,
    string? Fuente,
    string? Fecha,
    string? Comentario);
