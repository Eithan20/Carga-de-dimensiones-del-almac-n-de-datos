// API REST de comentarios de redes sociales.
// Expone los comentarios (guardados en data/social_comments.csv) con paginación y clave opcional.
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5080");
var app = builder.Build();

// Si Api:ApiKey tiene valor (user-secrets o variable de entorno), la API exige el encabezado X-Api-Key.
// Si no tiene valor, la API responde sin pedir clave.
var apiKey = app.Configuration["Api:ApiKey"];
var csvPath = Path.Combine(AppContext.BaseDirectory, "data", "social_comments.csv");
var comentarios = CargarComentarios(csvPath);

app.Logger.LogInformation("API lista: {Total} comentarios cargados. Autenticación por clave: {Auth}",
    comentarios.Count, string.IsNullOrEmpty(apiKey) ? "desactivada" : "activada");

app.MapGet("/api/comentarios", (HttpContext context, int? page, int? pageSize) =>
{
    if (!string.IsNullOrEmpty(apiKey) && context.Request.Headers["X-Api-Key"].ToString() != apiKey)
        return Results.Unauthorized();

    var p = Math.Max(page ?? 1, 1);
    var size = Math.Clamp(pageSize ?? 50, 1, 500);

    var items = comentarios
        .Skip((p - 1) * size)
        .Take(size)
        .Select(c => new
        {
            idComment = c.IdComment,
            idCliente = c.IdCliente,
            idProducto = c.IdProducto,
            fuente = c.Fuente,
            fecha = c.Fecha,
            comentario = c.Comentario
        })
        .ToList();

    return Results.Ok(new { page = p, pageSize = size, total = comentarios.Count, items });
});

app.Run();

static List<SocialComment> CargarComentarios(string path)
{
    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        MissingFieldFound = null,
        BadDataFound = null,
        HeaderValidated = null
    };

    using var reader = new StreamReader(path);
    using var csv = new CsvReader(reader, config);
    return csv.GetRecords<SocialComment>().ToList();
}

public class SocialComment
{
    [Name("IdComment")] public string? IdComment { get; set; }
    [Name("IdCliente")] public string? IdCliente { get; set; }
    [Name("IdProducto")] public string? IdProducto { get; set; }
    [Name("Fuente")] public string? Fuente { get; set; }
    [Name("Fecha")] public string? Fecha { get; set; }
    [Name("Comentario")] public string? Comentario { get; set; }
}