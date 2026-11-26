# Proceso ETL – Sistema de Análisis de Opiniones de Clientes

Worker Service en .NET 8 que hace dos cosas:

1. **Extracción:** obtiene opiniones de tres fuentes y las guarda en staging (SQL Server).
2. **Carga de dimensiones:** carga las dimensiones del almacén de datos (modelo en estrella) llamando a procedimientos almacenados con ADO.NET.

| Fuente | Clase | Tecnología |
|---|---|---|
| Archivo CSV (encuestas internas) | `CsvExtractor` | CsvHelper |
| Base de datos relacional (reseñas web) | `DatabaseExtractor` | ADO.NET (Microsoft.Data.SqlClient) |
| API REST (comentarios de redes sociales) | `ApiExtractor` | IHttpClientFactory |

| Dimensión | Procedimiento almacenado | Origen |
|---|---|---|
| `DimFecha` | `usp_CargarDimFecha` | Se genera según las fechas de las opiniones |
| `DimProducto` | `usp_CargarDimProducto` | `AnalisisOpinionesTransaccional.dbo.Productos` |
| `DimCliente` | `usp_CargarDimCliente` | `AnalisisOpinionesTransaccional.dbo.Clientes` |
| `DimFuente` | `usp_CargarDimFuente` | Valores fijos del modelo |
| `DimClasificacion` | `usp_CargarDimClasificacion` | Valores fijos del modelo |

El servicio los ejecuta con `SqlDimensionLoader` (`CommandType.StoredProcedure`) a través de `DimensionLoadService`.

## Estructura

```
src/
  OpinionesETL.Domain          interfaces (IExtractor, IStagingRepository, IDimensionLoader) y entidades
  OpinionesETL.Application     ExtractionOrchestrator y DimensionLoadService (casos de uso)
  OpinionesETL.Infrastructure  extractores, staging (SqlBulkCopy), carga de dimensiones (ADO.NET), configuración
  OpinionesETL.Worker          servicio en segundo plano, appsettings.json, Serilog
  OpinionesETL.SocialApi       API REST de comentarios sociales (sirve social_comments.csv)
sql/                           scripts de las bases, los procedimientos almacenados y la verificación
data/                          archivos CSV
```

## Requisitos

- .NET 8 SDK
- SQL Server (con SSMS)
- Base `AnalisisOpinionesTransaccional`: de ahí salen `DimProducto` y `DimCliente`. Se crea con el script `sql/00_crear_base_transaccional.sql`, que ya trae sus datos.

## Cómo ejecutarlo

1. En SSMS cree una base de datos vacía llamada `AnalisisOpinionesTransaccional` y, sobre ella, ejecute `sql/00_crear_base_transaccional.sql` (crea las tablas y carga los datos).
2. En SSMS ejecute `sql/01_crear_base_resenas_web.sql` (crea la base `FuenteResenasWeb`).
3. En SSMS ejecute `sql/02_crear_base_staging.sql` (crea la base `StagingOpiniones`).
4. En SSMS ejecute `sql/04_crear_base_analitica.sql` (crea la base `AnalisisOpinionesAnalitica` con sus tablas).
5. En SSMS ejecute `sql/05_cargar_dimensiones.sql` (crea los procedimientos almacenados).
6. En `src/OpinionesETL.Worker/appsettings.json`, en `ConnectionStrings`, cambie `Server=localhost` por el nombre del servidor que aparece en su SSMS (en las tres cadenas: `Staging`, `SourceDb` y `Warehouse`).
7. En una terminal, desde la carpeta del proyecto, inicie la API y déjela abierta (escucha en http://localhost:5080):
   ```
   dotnet run --project src/OpinionesETL.SocialApi
   ```
8. En otra terminal ejecute el servicio (extrae a staging y luego carga las dimensiones):
   ```
   dotnet run --project src/OpinionesETL.Worker
   ```
9. En SSMS ejecute `sql/03_verificar_extraccion.sql` para ver lo que quedó en staging y `sql/06_verificar_dimensiones.sql` para ver las dimensiones cargadas.

Para ver la API en el navegador: http://localhost:5080/api/comentarios?page=1&pageSize=5

El log queda en `src/OpinionesETL.Worker/logs/`.

## Credenciales

No hay contraseñas en el repositorio: SQL Server se usa con autenticación de Windows. La clave de la API es opcional; si se quiere proteger la API, se configura con user-secrets (mismo valor en los dos proyectos):

```
dotnet user-secrets set "Api:ApiKey" "su-clave" --project src/OpinionesETL.SocialApi
dotnet user-secrets set "Sources:Api:ApiKey" "su-clave" --project src/OpinionesETL.Worker
```

## Agregar una fuente nueva

1. Crear una clase que implemente `IExtractor`.
2. Crear su sección de opciones en `appsettings.json`.
3. Registrarla con una línea en `DependencyInjection.cs`.
