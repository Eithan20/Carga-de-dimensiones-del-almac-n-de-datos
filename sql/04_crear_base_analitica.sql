-- Base de datos analítica - Sistema de Análisis de Opiniones de Clientes (modelo en estrella)
-- Grupos de archivos: PRIMARY (.mdf) = dimensiones, FG_Secundario (.ndf) = tabla de hechos
-- Se puede ejecutar varias veces: recrea las tablas desde cero.

USE master;
GO

-- Crea la base con archivo primario, secundario y log 

IF DB_ID(N'AnalisisOpinionesAnalitica') IS NULL
BEGIN
    DECLARE @rutaDatos NVARCHAR(260) = CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(260));
    DECLARE @rutaLog NVARCHAR(260) = CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS NVARCHAR(260));

    IF @rutaDatos IS NULL OR @rutaLog IS NULL
    BEGIN
        DECLARE @carpetaMaster NVARCHAR(260) =
        (
            SELECT LEFT(physical_name, LEN(physical_name) - CHARINDEX('\', REVERSE(physical_name)) + 1)
            FROM master.sys.database_files
            WHERE file_id = 1
        );
        SET @rutaDatos = ISNULL(@rutaDatos, @carpetaMaster);
        SET @rutaLog = ISNULL(@rutaLog, @carpetaMaster);
    END;

    DECLARE @sql NVARCHAR(MAX) = N'
    CREATE DATABASE AnalisisOpinionesAnalitica
    ON PRIMARY
    (
        NAME = N''AnalisisOpinionesAnalitica_Primario'',
        FILENAME = N''' + @rutaDatos + N'AnalisisOpinionesAnalitica_Primario.mdf'',
        SIZE = 64MB,
        FILEGROWTH = 64MB
    ),
    FILEGROUP FG_Secundario
    (
        NAME = N''AnalisisOpinionesAnalitica_Secundario'',
        FILENAME = N''' + @rutaDatos + N'AnalisisOpinionesAnalitica_Secundario.ndf'',
        SIZE = 64MB,
        FILEGROWTH = 64MB
    )
    LOG ON
    (
        NAME = N''AnalisisOpinionesAnalitica_Log'',
        FILENAME = N''' + @rutaLog + N'AnalisisOpinionesAnalitica_Log.ldf'',
        SIZE = 32MB,
        FILEGROWTH = 32MB
    );';

    EXEC (@sql);
END;
GO

USE AnalisisOpinionesAnalitica;
GO

DROP TABLE IF EXISTS dbo.FactOpiniones;
DROP TABLE IF EXISTS dbo.DimClasificacion;
DROP TABLE IF EXISTS dbo.DimFuente;
DROP TABLE IF EXISTS dbo.DimCliente;
DROP TABLE IF EXISTS dbo.DimProducto;
DROP TABLE IF EXISTS dbo.DimFecha;
GO


-- DIMENSIONES (grupo PRIMARY)

CREATE TABLE dbo.DimFecha
(
    FechaKey INT NOT NULL,               -- AAAAMMDD, ejemplo 20250615
    Fecha DATE NOT NULL,
    Anio SMALLINT NOT NULL,
    Semestre TINYINT NOT NULL,
    Trimestre TINYINT NOT NULL,
    Mes TINYINT NOT NULL,
    NombreMes VARCHAR(15) NOT NULL,
    AnioMes CHAR(7) NOT NULL,            -- ejemplo 2025-06
    Dia TINYINT NOT NULL,
    DiaSemana TINYINT NOT NULL,
    NombreDia VARCHAR(15) NOT NULL,
    Evento NVARCHAR(100) NULL,           -- campaña o lanzamiento de ese día

    CONSTRAINT PK_DimFecha PRIMARY KEY (FechaKey),
    CONSTRAINT UQ_DimFecha_Fecha UNIQUE (Fecha),
    CONSTRAINT CK_DimFecha_Semestre CHECK (Semestre BETWEEN 1 AND 2),
    CONSTRAINT CK_DimFecha_Trimestre CHECK (Trimestre BETWEEN 1 AND 4),
    CONSTRAINT CK_DimFecha_Mes CHECK (Mes BETWEEN 1 AND 12)
) ON [PRIMARY];
GO

CREATE TABLE dbo.DimProducto
(
    ProductoKey INT IDENTITY(1,1) NOT NULL,
    IdProductoOrigen INT NOT NULL,       -- Id en la base transaccional
    Nombre NVARCHAR(100) NOT NULL,
    Categoria NVARCHAR(50) NOT NULL,

    CONSTRAINT PK_DimProducto PRIMARY KEY (ProductoKey),
    CONSTRAINT UQ_DimProducto_Origen UNIQUE (IdProductoOrigen)
) ON [PRIMARY];
GO

CREATE TABLE dbo.DimCliente
(
    ClienteKey INT IDENTITY(1,1) NOT NULL,   -- la clave 0 es "Cliente anónimo"
    IdClienteOrigen INT NULL,                -- Id en la base transaccional
    Nombre NVARCHAR(100) NOT NULL,
    Pais NVARCHAR(50) NOT NULL
        CONSTRAINT DF_DimCliente_Pais DEFAULT ('No especificado'),
    RangoEdad VARCHAR(20) NOT NULL
        CONSTRAINT DF_DimCliente_RangoEdad DEFAULT ('No especificado'),
    TipoCliente VARCHAR(30) NOT NULL
        CONSTRAINT DF_DimCliente_Tipo DEFAULT ('No especificado'),

    CONSTRAINT PK_DimCliente PRIMARY KEY (ClienteKey),
    CONSTRAINT UQ_DimCliente_Origen UNIQUE (IdClienteOrigen)
) ON [PRIMARY];
GO

CREATE TABLE dbo.DimFuente
(
    FuenteKey INT IDENTITY(1,1) NOT NULL,
    Canal VARCHAR(20) NOT NULL,
    Plataforma VARCHAR(30) NOT NULL,

    CONSTRAINT PK_DimFuente PRIMARY KEY (FuenteKey),
    CONSTRAINT UQ_DimFuente_Canal_Plataforma UNIQUE (Canal, Plataforma),
    CONSTRAINT CK_DimFuente_Canal CHECK (Canal IN ('Encuesta', 'ResenaWeb', 'ComentarioSocial'))
) ON [PRIMARY];
GO

CREATE TABLE dbo.DimClasificacion
(
    ClasificacionKey INT IDENTITY(1,1) NOT NULL,
    Clasificacion VARCHAR(20) NOT NULL,
    Criterio VARCHAR(200) NOT NULL,

    CONSTRAINT PK_DimClasificacion PRIMARY KEY (ClasificacionKey),
    CONSTRAINT UQ_DimClasificacion_Nombre UNIQUE (Clasificacion),
    CONSTRAINT CK_DimClasificacion_Valor
        CHECK (Clasificacion IN ('Positiva', 'Neutra', 'Negativa', 'Sin clasificar'))
) ON [PRIMARY];
GO


-- TABLA DE HECHOS (grupo FG_Secundario). Una fila por opinión.

CREATE TABLE dbo.FactOpiniones
(
    OpinionKey BIGINT IDENTITY(1,1) NOT NULL,

    FechaKey INT NOT NULL,
    ProductoKey INT NOT NULL,
    ClienteKey INT NOT NULL
        CONSTRAINT DF_FactOpiniones_Cliente DEFAULT (0),
    FuenteKey INT NOT NULL,
    ClasificacionKey INT NOT NULL,

    IdOpinionOrigen BIGINT NOT NULL,     -- Id en la base transaccional (ahí está el comentario)

    PuntajeSatisfaccion TINYINT NULL,    -- 1 a 5; NULL si la fuente no trae puntaje
    CantidadComentarios INT NOT NULL
        CONSTRAINT DF_FactOpiniones_Cantidad DEFAULT (1),
    EsSatisfecho AS (
        CASE
            WHEN PuntajeSatisfaccion IS NULL THEN NULL
            WHEN PuntajeSatisfaccion >= 4 THEN 1
            ELSE 0
        END
    ) PERSISTED,

    CONSTRAINT PK_FactOpiniones PRIMARY KEY (OpinionKey),
    CONSTRAINT FK_FactOpiniones_DimFecha
        FOREIGN KEY (FechaKey) REFERENCES dbo.DimFecha(FechaKey),
    CONSTRAINT FK_FactOpiniones_DimProducto
        FOREIGN KEY (ProductoKey) REFERENCES dbo.DimProducto(ProductoKey),
    CONSTRAINT FK_FactOpiniones_DimCliente
        FOREIGN KEY (ClienteKey) REFERENCES dbo.DimCliente(ClienteKey),
    CONSTRAINT FK_FactOpiniones_DimFuente
        FOREIGN KEY (FuenteKey) REFERENCES dbo.DimFuente(FuenteKey),
    CONSTRAINT FK_FactOpiniones_DimClasificacion
        FOREIGN KEY (ClasificacionKey) REFERENCES dbo.DimClasificacion(ClasificacionKey),
    CONSTRAINT UQ_FactOpiniones_Origen UNIQUE (FuenteKey, IdOpinionOrigen),
    CONSTRAINT CK_FactOpiniones_Puntaje
        CHECK (PuntajeSatisfaccion IS NULL OR PuntajeSatisfaccion BETWEEN 1 AND 5),
    CONSTRAINT CK_FactOpiniones_Cantidad CHECK (CantidadComentarios >= 1)
) ON FG_Secundario;
GO

CREATE INDEX IX_FactOpiniones_Fecha ON dbo.FactOpiniones (FechaKey) ON FG_Secundario;
CREATE INDEX IX_FactOpiniones_Producto_Fecha ON dbo.FactOpiniones (ProductoKey, FechaKey) ON FG_Secundario;
CREATE INDEX IX_FactOpiniones_Cliente ON dbo.FactOpiniones (ClienteKey) ON FG_Secundario;
CREATE INDEX IX_FactOpiniones_Fuente ON dbo.FactOpiniones (FuenteKey) ON FG_Secundario;
CREATE INDEX IX_FactOpiniones_Clasificacion ON dbo.FactOpiniones (ClasificacionKey) ON FG_Secundario;
GO


-- DATOS BASE

INSERT INTO dbo.DimClasificacion (Clasificacion, Criterio)
VALUES
    ('Positiva', 'Puntaje 4 o 5'),
    ('Neutra', 'Puntaje 3'),
    ('Negativa', 'Puntaje 1 o 2'),
    ('Sin clasificar', 'Opinión sin puntaje (por ejemplo, redes sociales)');

INSERT INTO dbo.DimFuente (Canal, Plataforma)
VALUES
    ('Encuesta', 'EncuestaInterna'),
    ('ResenaWeb', 'SitioWeb'),
    ('ComentarioSocial', 'Instagram'),
    ('ComentarioSocial', 'Facebook'),
    ('ComentarioSocial', 'Twitter');

SET IDENTITY_INSERT dbo.DimCliente ON;
INSERT INTO dbo.DimCliente (ClienteKey, IdClienteOrigen, Nombre)
VALUES (0, NULL, 'Cliente anónimo');
SET IDENTITY_INSERT dbo.DimCliente OFF;
GO


-- VERIFICACIÓN: archivos de la base y grupo de archivos de cada tabla

SELECT f.name AS NombreLogico, ISNULL(g.name, 'LOG') AS GrupoArchivos, f.type_desc AS Tipo,
       f.physical_name AS ArchivoFisico, f.size * 8 / 1024 AS TamanoMB
FROM sys.database_files f
LEFT JOIN sys.filegroups g ON g.data_space_id = f.data_space_id
ORDER BY f.type, f.file_id;

SELECT t.name AS Tabla, ds.name AS GrupoArchivos
FROM sys.tables t
JOIN sys.indexes i ON i.object_id = t.object_id AND i.index_id IN (0, 1)
JOIN sys.data_spaces ds ON ds.data_space_id = i.data_space_id
ORDER BY ds.name, t.name;
GO
