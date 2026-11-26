using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using OpinionesETL.Domain.Entities;
using OpinionesETL.Domain.Interfaces;
using OpinionesETL.Infrastructure.Configuration;

namespace OpinionesETL.Infrastructure.Warehouse;

/// <summary>
/// Carga las dimensiones del almacén con ADO.NET: ejecuta el procedimiento almacenado
/// dbo.usp_Cargar{dimensión} de la base AnalisisOpinionesAnalitica y lee el resumen que devuelve.
/// </summary>
public sealed class SqlDimensionLoader : IDimensionLoader
{
    // Solo se pueden ejecutar procedimientos de esta lista.
    private static readonly HashSet<string> Permitidas = new(StringComparer.Ordinal)
    {
        "DimFecha", "DimProducto", "DimCliente", "DimFuente", "DimClasificacion"
    };

    private readonly string _connectionString;
    private readonly int _timeoutSeconds;

    public SqlDimensionLoader(IOptions<ConnectionStringsOptions> connections, IOptions<WarehouseOptions> options)
    {
        _connectionString = connections.Value.Warehouse;
        _timeoutSeconds = options.Value.CommandTimeoutSeconds;
    }

    public async Task<DimensionLoadResult> LoadAsync(string dimension, CancellationToken cancellationToken = default)
    {
        if (!Permitidas.Contains(dimension))
            throw new ArgumentException($"Dimensión no reconocida: {dimension}", nameof(dimension));

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand($"dbo.usp_Cargar{dimension}", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _timeoutSeconds
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException($"El procedimiento de {dimension} no devolvió resultado.");

        return new DimensionLoadResult(
            reader.GetString(reader.GetOrdinal("Dimension")),
            Convert.ToInt32(reader["Insertados"]),
            Convert.ToInt32(reader["Actualizados"]),
            Convert.ToInt32(reader["TotalEnDimension"]));
    }
}
