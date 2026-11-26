using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Domain.Entities;
using OpinionesETL.Domain.Interfaces;

namespace OpinionesETL.Application;

/// <summary>
/// Caso de uso principal: ejecuta todos los extractores registrados en paralelo y guarda
/// lo extraído en staging por bloques. No conoce CSV, SQL ni HTTP: solo las interfaces.
/// </summary>
public sealed class ExtractionOrchestrator
{
    private readonly IReadOnlyList<IExtractor> _extractors;
    private readonly IStagingRepository _staging;
    private readonly ExtractionOptions _options;
    private readonly ILogger<ExtractionOrchestrator> _logger;

    public ExtractionOrchestrator(
        IEnumerable<IExtractor> extractors,
        IStagingRepository staging,
        IOptions<ExtractionOptions> options,
        ILogger<ExtractionOrchestrator> logger)
    {
        _extractors = extractors.ToList();
        _staging = staging;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SourceResult>> RunAsync(CancellationToken cancellationToken)
    {
        if (_extractors.Count == 0)
        {
            _logger.LogWarning("No hay fuentes habilitadas. Revise la sección Sources de appsettings.json.");
            return Array.Empty<SourceResult>();
        }

        _logger.LogInformation("Extracción iniciada con {Count} fuente(s): {Fuentes}",
            _extractors.Count, string.Join(", ", _extractors.Select(e => e.SourceName)));

        var total = Stopwatch.StartNew();
        using var gate = new SemaphoreSlim(Math.Max(1, _options.MaxDegreeOfParallelism));

        // Una tarea por fuente: se ejecutan en paralelo y un fallo en una no detiene a las demás.
        var tasks = _extractors.Select(e => RunSourceAsync(e, gate, cancellationToken)).ToList();
        var results = await Task.WhenAll(tasks);
        total.Stop();

        foreach (var r in results)
        {
            _logger.LogInformation(
                "Resumen | fuente={Fuente} estado={Estado} registros={Registros} tiempo={Tiempo:F2}s velocidad={Velocidad:F0} reg/s",
                r.Source, r.Status, r.Records, r.Duration.TotalSeconds, r.RecordsPerSecond);
        }

        _logger.LogInformation("Extracción finalizada: {Total} registros en {Tiempo:F2}s ({Errores} fuente(s) con error)",
            results.Sum(r => r.Records), total.Elapsed.TotalSeconds, results.Count(r => r.Status == EstadoLote.Error));

        return results;
    }

    private async Task<SourceResult> RunSourceAsync(IExtractor extractor, SemaphoreSlim gate, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        var timer = Stopwatch.StartNew();
        long? loteId = null;
        long count = 0;

        try
        {
            loteId = await _staging.StartBatchAsync(extractor.SourceName, extractor.Canal, ct);
            _logger.LogInformation("[{Fuente}] Extracción iniciada (lote {Lote})", extractor.SourceName, loteId);

            var buffer = new List<RawOpinion>(_options.BatchSize);

            await foreach (var opinion in extractor.ExtractAsync(ct).WithCancellation(ct))
            {
                buffer.Add(opinion);
                if (buffer.Count >= _options.BatchSize)
                {
                    await _staging.WriteAsync(loteId.Value, buffer, ct);
                    count += buffer.Count;
                    buffer.Clear();
                    _logger.LogDebug("[{Fuente}] {Count} registros guardados hasta ahora", extractor.SourceName, count);
                }
            }

            if (buffer.Count > 0)
            {
                await _staging.WriteAsync(loteId.Value, buffer, ct);
                count += buffer.Count;
            }

            timer.Stop();
            await _staging.FinishBatchAsync(loteId.Value, EstadoLote.Completado, count,
                (long)timer.Elapsed.TotalMilliseconds, null, ct);

            _logger.LogInformation("[{Fuente}] Extracción completada: {Count} registros", extractor.SourceName, count);
            return new SourceResult(extractor.SourceName, extractor.Canal, EstadoLote.Completado, count, timer.Elapsed, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("[{Fuente}] Extracción cancelada", extractor.SourceName);
            await TryFinishAsync(loteId, EstadoLote.Cancelado, count, timer, null);
            throw;
        }
        catch (Exception ex)
        {
            timer.Stop();
            _logger.LogError(ex, "[{Fuente}] La extracción falló: {Mensaje}", extractor.SourceName, ex.Message);
            await TryFinishAsync(loteId, EstadoLote.Error, count, timer, ex.Message);
            return new SourceResult(extractor.SourceName, extractor.Canal, EstadoLote.Error, count, timer.Elapsed, ex.Message);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Intenta cerrar el lote sin dejar que un error aquí oculte el error original.</summary>
    private async Task TryFinishAsync(long? loteId, string estado, long count, Stopwatch timer, string? error)
    {
        if (loteId is null) return;
        try
        {
            await _staging.FinishBatchAsync(loteId.Value, estado, count,
                (long)timer.Elapsed.TotalMilliseconds, error, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo cerrar el lote {Lote}", loteId);
        }
    }
}
