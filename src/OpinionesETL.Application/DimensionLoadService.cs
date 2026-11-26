using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Domain.Interfaces;

namespace OpinionesETL.Application;

/// <summary>Resultado de cargar una dimensión.</summary>
public sealed record DimensionOutcome(
    string Dimension,
    string Status,
    int Inserted,
    int Updated,
    int Total,
    TimeSpan Duration,
    string? Error);

/// <summary>
/// Carga las dimensiones del almacén una por una, en orden, llamando a sus procedimientos
/// almacenados a través de IDimensionLoader. Mide cada una con Stopwatch y registra el resultado.
/// Si una dimensión falla, se registra el error y se continúa con las demás.
/// </summary>
public sealed class DimensionLoadService
{
    /// <summary>Orden de carga. Cada nombre corresponde al procedimiento dbo.usp_Cargar{nombre}.</summary>
    public static readonly string[] Dimensions =
        { "DimFecha", "DimProducto", "DimCliente", "DimFuente", "DimClasificacion" };

    private readonly IDimensionLoader _loader;
    private readonly DimensionLoadOptions _options;
    private readonly ILogger<DimensionLoadService> _logger;

    public DimensionLoadService(IDimensionLoader loader, IOptions<DimensionLoadOptions> options,
        ILogger<DimensionLoadService> logger)
    {
        _loader = loader;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DimensionOutcome>> RunAsync(CancellationToken cancellationToken = default)
    {
        var outcomes = new List<DimensionOutcome>();

        if (!_options.Enabled)
        {
            _logger.LogInformation("Carga de dimensiones desactivada (DimensionLoad:Enabled = false)");
            return outcomes;
        }

        _logger.LogInformation("Carga de dimensiones iniciada: {Dimensiones}", string.Join(", ", Dimensions));
        var total = Stopwatch.StartNew();

        foreach (var dimension in Dimensions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timer = Stopwatch.StartNew();

            try
            {
                var result = await _loader.LoadAsync(dimension, cancellationToken);
                timer.Stop();

                outcomes.Add(new DimensionOutcome(dimension, "Completado", result.Inserted, result.Updated,
                    result.Total, timer.Elapsed, null));

                _logger.LogInformation(
                    "[{Dimension}] Carga completada | insertados={Insertados} actualizados={Actualizados} total={Total} tiempo={Tiempo:F2}s",
                    dimension, result.Inserted, result.Updated, result.Total, timer.Elapsed.TotalSeconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                timer.Stop();
                outcomes.Add(new DimensionOutcome(dimension, "Error", 0, 0, 0, timer.Elapsed, ex.Message));
                _logger.LogError(ex, "[{Dimension}] Falló la carga de la dimensión", dimension);
            }
        }

        total.Stop();
        var fallidas = outcomes.Count(o => o.Status == "Error");
        _logger.LogInformation(
            "Carga de dimensiones finalizada: {Completadas} completada(s), {Fallidas} con error, tiempo={Tiempo:F2}s",
            outcomes.Count - fallidas, fallidas, total.Elapsed.TotalSeconds);

        return outcomes;
    }
}
