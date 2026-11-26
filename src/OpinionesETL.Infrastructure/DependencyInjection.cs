using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpinionesETL.Domain.Interfaces;
using OpinionesETL.Infrastructure.Configuration;
using OpinionesETL.Infrastructure.Extractors;
using OpinionesETL.Infrastructure.Staging;
using OpinionesETL.Infrastructure.Warehouse;

namespace OpinionesETL.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra las fuentes habilitadas en appsettings.json. Para agregar una fuente nueva:
    /// 1) crear su clase IExtractor, 2) crear su sección de opciones y 3) agregar una línea aquí.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ConnectionStringsOptions>(configuration.GetSection(ConnectionStringsOptions.SectionName));
        services.Configure<CsvSourceOptions>(configuration.GetSection(CsvSourceOptions.SectionName));
        services.Configure<DatabaseSourceOptions>(configuration.GetSection(DatabaseSourceOptions.SectionName));
        services.Configure<ApiSourceOptions>(configuration.GetSection(ApiSourceOptions.SectionName));

        services.AddSingleton<IStagingRepository, SqlStagingRepository>();

        // Carga de dimensiones: ADO.NET llamando a procedimientos almacenados del almacén de datos.
        services.Configure<WarehouseOptions>(configuration.GetSection(WarehouseOptions.SectionName));
        services.AddSingleton<IDimensionLoader, SqlDimensionLoader>();

        var csv = configuration.GetSection(CsvSourceOptions.SectionName).Get<CsvSourceOptions>() ?? new CsvSourceOptions();
        var db = configuration.GetSection(DatabaseSourceOptions.SectionName).Get<DatabaseSourceOptions>() ?? new DatabaseSourceOptions();
        var api = configuration.GetSection(ApiSourceOptions.SectionName).Get<ApiSourceOptions>() ?? new ApiSourceOptions();

        if (csv.Enabled) services.AddSingleton<IExtractor, CsvExtractor>();
        if (db.Enabled) services.AddSingleton<IExtractor, DatabaseExtractor>();
        if (api.Enabled) services.AddSingleton<IExtractor, ApiExtractor>();

        // Cliente HTTP administrado por IHttpClientFactory (reutiliza conexiones, evita agotar sockets).
        services.AddHttpClient(ApiExtractor.ClientName, (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<ApiSourceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            // La clave es opcional: si existe (user-secrets o variable de entorno) se envía en cada llamada.
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
                client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
        });

        return services;
    }
}
