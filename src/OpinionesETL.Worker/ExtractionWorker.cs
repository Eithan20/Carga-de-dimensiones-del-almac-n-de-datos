using Microsoft.Extensions.Options;
using OpinionesETL.Application;

namespace OpinionesETL.Worker;

/// <summary>
/// Servicio en segundo plano. Solo decide cuándo se ejecuta el ciclo: la extracción a staging
/// (ExtractionOrchestrator) y luego la carga de dimensiones (DimensionLoadService).
/// </summary>
public sealed class ExtractionWorker : BackgroundService
{
    private readonly ExtractionOrchestrator _orchestrator;
    private readonly DimensionLoadService _dimensionLoad;
    private readonly ExtractionOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ExtractionWorker> _logger;

    public ExtractionWorker(
        ExtractionOrchestrator orchestrator,
        DimensionLoadService dimensionLoad,
        IOptions<ExtractionOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<ExtractionWorker> logger)
    {
        _orchestrator = orchestrator;
        _dimensionLoad = dimensionLoad;
        _options = options.Value;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            do
            {
                _logger.LogInformation("Ciclo de extracción iniciado a las {Hora:HH:mm:ss}", DateTime.Now);

                try
                {
                    await _orchestrator.RunAsync(stoppingToken);
                    await _dimensionLoad.RunAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "El ciclo de extracción terminó con un error inesperado");
                }

                if (_options.RunOnce) break;

                _logger.LogInformation("Próxima extracción en {Minutos} minuto(s)", _options.IntervalMinutes);
                await Task.Delay(TimeSpan.FromMinutes(_options.IntervalMinutes), stoppingToken);
            }
            while (!stoppingToken.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            // Apagado normal del servicio.
        }
        finally
        {
            if (_options.RunOnce)
            {
                _logger.LogInformation("Extracción única terminada. Cerrando el servicio.");
                _lifetime.StopApplication();
            }
        }
    }
}
