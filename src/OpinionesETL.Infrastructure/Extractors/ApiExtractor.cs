using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Domain.Entities;
using OpinionesETL.Domain.Interfaces;
using OpinionesETL.Infrastructure.Configuration;

namespace OpinionesETL.Infrastructure.Extractors;

/// <summary>Extrae los comentarios de redes sociales consumiendo una API REST paginada.</summary>
public sealed class ApiExtractor : IExtractor
{
    /// <summary>Nombre del HttpClient configurado en DependencyInjection.</summary>
    public const string ClientName = "SocialApi";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApiSourceOptions _options;
    private readonly ILogger<ApiExtractor> _logger;

    public ApiExtractor(
        IHttpClientFactory httpClientFactory,
        IOptions<ApiSourceOptions> options,
        ILogger<ApiExtractor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string SourceName => "API";

    public string Canal => _options.Canal;

    public async IAsyncEnumerable<RawOpinion> ExtractAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(ClientName);
        var origen = $"{_options.BaseUrl.TrimEnd('/')}{_options.Endpoint}";

        var page = 1;
        long received = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = $"{_options.Endpoint}?page={page}&pageSize={_options.PageSize}";
            var body = await GetPageAsync(client, url, cancellationToken);

            if (body?.Items is null || body.Items.Count == 0)
                yield break;

            foreach (var item in body.Items)
            {
                yield return new RawOpinion(
                    Canal: _options.Canal,
                    Origen: origen,
                    IdOrigen: item.IdComment,
                    IdCliente: item.IdCliente,
                    IdProducto: item.IdProducto,
                    Fecha: item.Fecha,
                    Comentario: item.Comentario,
                    Clasificacion: null,
                    Puntaje: null,
                    Plataforma: item.Fuente);
            }

            received += body.Items.Count;
            _logger.LogDebug("[API] Página {Page}: {Count} comentarios ({Received}/{Total})",
                page, body.Items.Count, received, body.Total);

            if (received >= body.Total)
                yield break;

            page++;
        }
    }

    /// <summary>Pide una página con reintentos (espera creciente) ante fallos temporales.</summary>
    private async Task<ApiPage?> GetPageAsync(HttpClient client, string url, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ApiPage>(JsonOptions, ct);
            }
            catch (Exception ex) when (attempt < _options.MaxRetries && !ct.IsCancellationRequested && IsTransient(ex))
            {
                var wait = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                _logger.LogWarning("[API] Intento {Attempt}/{Max} falló ({Mensaje}). Reintento en {Wait}s",
                    attempt, _options.MaxRetries, ex.Message, wait.TotalSeconds);
                await Task.Delay(wait, ct);
            }
        }
    }

    /// <summary>Errores de red, tiempos agotados y 5xx se reintentan; un 401 o 404 no.</summary>
    private static bool IsTransient(Exception ex) => ex switch
    {
        HttpRequestException http => http.StatusCode is null || (int)http.StatusCode >= 500,
        TaskCanceledException => true,
        _ => false
    };
}
