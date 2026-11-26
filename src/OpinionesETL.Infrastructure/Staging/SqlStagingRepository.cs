using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Domain.Entities;
using OpinionesETL.Domain.Interfaces;
using OpinionesETL.Infrastructure.Configuration;

namespace OpinionesETL.Infrastructure.Staging;

/// <summary>Guarda lo extraído en las tablas de staging de SQL Server usando SqlBulkCopy.</summary>
public sealed class SqlStagingRepository : IStagingRepository
{
    private const string TablaDestino = "stg.OpinionesCrudas";

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _connectionString;
    private readonly ILogger<SqlStagingRepository> _logger;

    public SqlStagingRepository(IOptions<ConnectionStringsOptions> options, ILogger<SqlStagingRepository> logger)
    {
        _connectionString = options.Value.Staging;
        _logger = logger;
    }

    public async Task<long> StartBatchAsync(string fuente, string canal, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO stg.LoteExtraccion (Fuente, Canal, Estado)
            OUTPUT INSERTED.IdLote
            VALUES (@Fuente, @Canal, @Estado);";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Fuente", fuente);
        command.Parameters.AddWithValue("@Canal", canal);
        command.Parameters.AddWithValue("@Estado", EstadoLote.EnCurso);

        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(id);
    }

    public async Task WriteAsync(long loteId, IReadOnlyCollection<RawOpinion> opiniones,
        CancellationToken cancellationToken = default)
    {
        if (opiniones.Count == 0) return;

        var table = new DataTable();
        table.Columns.Add("IdLote", typeof(long));
        table.Columns.Add("Canal", typeof(string));
        table.Columns.Add("Origen", typeof(string));
        table.Columns.Add("IdOrigen", typeof(string));
        table.Columns.Add("IdCliente", typeof(string));
        table.Columns.Add("IdProducto", typeof(string));
        table.Columns.Add("Fecha", typeof(string));
        table.Columns.Add("Comentario", typeof(string));
        table.Columns.Add("Clasificacion", typeof(string));
        table.Columns.Add("Puntaje", typeof(string));
        table.Columns.Add("Plataforma", typeof(string));

        foreach (var o in opiniones)
        {
            table.Rows.Add(loteId, o.Canal, o.Origen,
                (object?)o.IdOrigen ?? DBNull.Value,
                (object?)o.IdCliente ?? DBNull.Value,
                (object?)o.IdProducto ?? DBNull.Value,
                (object?)o.Fecha ?? DBNull.Value,
                (object?)o.Comentario ?? DBNull.Value,
                (object?)o.Clasificacion ?? DBNull.Value,
                (object?)o.Puntaje ?? DBNull.Value,
                (object?)o.Plataforma ?? DBNull.Value);
        }

        // Las fuentes se extraen en paralelo, pero la escritura a la misma tabla de staging se hace
        // de una en una: evita que dos cargas masivas choquen entre sí sobre la tabla.
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var bulk = new SqlBulkCopy(connection)
            {
                DestinationTableName = TablaDestino,
                BulkCopyTimeout = 120
            };
            foreach (DataColumn column in table.Columns)
                bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);

            await bulk.WriteToServerAsync(table, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }

        _logger.LogDebug("Lote {Lote}: {Count} filas enviadas a {Tabla}", loteId, opiniones.Count, TablaDestino);
    }

    public async Task FinishBatchAsync(long loteId, string estado, long registros, long duracionMs, string? error,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE stg.LoteExtraccion
            SET Estado = @Estado, Registros = @Registros, DuracionMs = @Duracion,
                Fin = SYSDATETIME(), Error = @Error
            WHERE IdLote = @IdLote;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Estado", estado);
        command.Parameters.AddWithValue("@Registros", registros);
        command.Parameters.AddWithValue("@Duracion", duracionMs);
        command.Parameters.AddWithValue("@Error", (object?)Truncate(error, 2000) ?? DBNull.Value);
        command.Parameters.AddWithValue("@IdLote", loteId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}