namespace OpinionesETL.Infrastructure.Configuration;

/// <summary>Cadenas de conexión (sección "ConnectionStrings"). Sin contraseñas: autenticación de Windows.</summary>
public sealed class ConnectionStringsOptions
{
    public const string SectionName = "ConnectionStrings";

    /// <summary>Base donde se guarda el staging.</summary>
    public string Staging { get; set; } = string.Empty;

    /// <summary>Base relacional de donde se leen las reseñas.</summary>
    public string SourceDb { get; set; } = string.Empty;

    /// <summary>Almacén de datos (AnalisisOpinionesAnalitica) donde se cargan las dimensiones.</summary>
    public string Warehouse { get; set; } = string.Empty;
}

/// <summary>Fuente CSV (sección "Sources:Csv").</summary>
public sealed class CsvSourceOptions
{
    public const string SectionName = "Sources:Csv";

    public bool Enabled { get; set; } = true;

    /// <summary>Ruta del archivo. Si es relativa, se toma desde la carpeta del programa.</summary>
    public string Path { get; set; } = string.Empty;

    public string Delimiter { get; set; } = ",";

    public string Canal { get; set; } = "Encuesta";
}

/// <summary>Fuente base de datos relacional (sección "Sources:Database").</summary>
public sealed class DatabaseSourceOptions
{
    public const string SectionName = "Sources:Database";

    public bool Enabled { get; set; } = true;

    /// <summary>Consulta que devuelve: IdReview, IdCliente, IdProducto, Fecha, Comentario, Rating.</summary>
    public string Query { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; set; } = 60;

    public string Canal { get; set; } = "ResenaWeb";
}

/// <summary>Fuente API REST (sección "Sources:Api").</summary>
public sealed class ApiSourceOptions
{
    public const string SectionName = "Sources:Api";

    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = string.Empty;

    public string Endpoint { get; set; } = "/api/comentarios";

    public int PageSize { get; set; } = 50;

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxRetries { get; set; } = 3;

    /// <summary>Clave de la API. No se escribe en appsettings.json: va en user-secrets o en una variable de entorno.</summary>
    public string? ApiKey { get; set; }

    public string Canal { get; set; } = "ComentarioSocial";
}
