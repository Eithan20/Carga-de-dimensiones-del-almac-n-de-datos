using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Domain.Entities;
using OpinionesETL.Domain.Interfaces;
using OpinionesETL.Infrastructure.Configuration;

namespace OpinionesETL.Infrastructure.Extractors;

/// <summary>Extrae las reseñas web desde una base de datos relacional (SQL Server) con ADO.NET.</summary>
public sealed class DatabaseExtractor : IExtractor
{
    private readonly DatabaseSourceOptions _options;
    private readonly ConnectionStringsOptions _connections;
    private readonly ILogger<DatabaseExtractor> _logger;

    public DatabaseExtractor(
        IOptions<DatabaseSourceOptions> options,
        IOptions<ConnectionStringsOptions> connections,
        ILogger<DatabaseExtractor> logger)
    {
        _options = options.Value;
        _connections = connections.Value;
        _logger = logger;
    }

    public string SourceName => "BaseDatos";

    public string Canal => _options.Canal;

    public async IAsyncEnumerable<RawOpinion> ExtractAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connections.SourceDb))
            throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:SourceDb.");
        if (string.IsNullOrWhiteSpace(_options.Query))
            throw new InvalidOperationException("Falta la consulta Sources:Database:Query.");

        await using var connection = new SqlConnection(_connections.SourceDb);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(_options.Query, connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };

        // El lector avanza fila por fila: no se carga toda la tabla en memoria.
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var iId = reader.GetOrdinal("IdReview");
        var iCliente = reader.GetOrdinal("IdCliente");
        var iProducto = reader.GetOrdinal("IdProducto");
        var iFecha = reader.GetOrdinal("Fecha");
        var iComentario = reader.GetOrdinal("Comentario");
        var iRating = reader.GetOrdinal("Rating");

        var origen = $"{connection.Database}.ResenasWeb";
        long rows = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            rows++;
            yield return new RawOpinion(
                Canal: _options.Canal,
                Origen: origen,
                IdOrigen: Text(reader, iId),
                IdCliente: Text(reader, iCliente),
                IdProducto: Text(reader, iProducto),
                Fecha: Text(reader, iFecha),
                Comentario: Text(reader, iComentario),
                Clasificacion: null,
                Puntaje: Text(reader, iRating),
                Plataforma: "SitioWeb");
        }

        _logger.LogDebug("[BaseDatos] {Rows} filas leídas de {Origen}", rows, origen);
    }

    private static string? Text(SqlDataReader reader, int index)
    {
        if (reader.IsDBNull(index)) return null;
        var value = Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture)?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
