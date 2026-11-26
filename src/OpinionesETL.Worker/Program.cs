using OpinionesETL.Application;
using OpinionesETL.Infrastructure;
using OpinionesETL.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Serilog se configura desde appsettings.json (consola + archivo en la carpeta logs).
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger, dispose: false);

builder.Services.Configure<ExtractionOptions>(builder.Configuration.GetSection(ExtractionOptions.SectionName));
builder.Services.AddSingleton<ExtractionOrchestrator>();
builder.Services.Configure<DimensionLoadOptions>(builder.Configuration.GetSection(DimensionLoadOptions.SectionName));
builder.Services.AddSingleton<DimensionLoadService>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ExtractionWorker>();

var host = builder.Build();

try
{
    await host.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "El servicio terminó por un error no controlado");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
