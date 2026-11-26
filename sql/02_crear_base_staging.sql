-- ============================================================
-- Staging de la extracción (destino del Worker Service)
-- Aquí se guardan los datos crudos, tal como salen de cada fuente.
-- Se puede ejecutar varias veces: no borra datos existentes.
-- ============================================================
USE master;
GO

IF DB_ID(N'StagingOpiniones') IS NULL
    CREATE DATABASE StagingOpiniones;
GO

USE StagingOpiniones;
GO

IF SCHEMA_ID(N'stg') IS NULL
    EXEC (N'CREATE SCHEMA stg');
GO

-- Una fila por cada extracción de una fuente (auditoría y métricas).
IF OBJECT_ID(N'stg.LoteExtraccion', N'U') IS NULL
CREATE TABLE stg.LoteExtraccion
(
    IdLote     BIGINT IDENTITY(1,1) NOT NULL,
    Fuente     VARCHAR(30)   NOT NULL,                       -- CSV, BaseDatos, API
    Canal      VARCHAR(20)   NOT NULL,                       -- Encuesta, ResenaWeb, ComentarioSocial
    Estado     VARCHAR(15)   NOT NULL,                       -- EnCurso, Completado, Error, Cancelado
    Registros  BIGINT        NOT NULL CONSTRAINT DF_Lote_Registros DEFAULT (0),
    Inicio     DATETIME2(3)  NOT NULL CONSTRAINT DF_Lote_Inicio DEFAULT (SYSDATETIME()),
    Fin        DATETIME2(3)  NULL,
    DuracionMs BIGINT        NULL,
    Error      NVARCHAR(2000) NULL,

    CONSTRAINT PK_LoteExtraccion PRIMARY KEY (IdLote),
    CONSTRAINT CK_Lote_Estado CHECK (Estado IN ('EnCurso', 'Completado', 'Error', 'Cancelado'))
);
GO

-- Datos crudos de las tres fuentes. Todo es texto: la limpieza se hace en la etapa de transformación.
IF OBJECT_ID(N'stg.OpinionesCrudas', N'U') IS NULL
CREATE TABLE stg.OpinionesCrudas
(
    IdFila          BIGINT IDENTITY(1,1) NOT NULL,
    IdLote          BIGINT         NOT NULL,
    Canal           VARCHAR(20)    NOT NULL,
    Origen          NVARCHAR(260)  NULL,                     -- archivo, tabla o endpoint
    IdOrigen        NVARCHAR(100)  NULL,
    IdCliente       NVARCHAR(100)  NULL,
    IdProducto      NVARCHAR(100)  NULL,
    Fecha           NVARCHAR(100)  NULL,
    Comentario      NVARCHAR(MAX)  NULL,
    Clasificacion   NVARCHAR(100)  NULL,
    Puntaje         NVARCHAR(100)  NULL,
    Plataforma      NVARCHAR(100)  NULL,
    FechaExtraccion DATETIME2(3)   NOT NULL CONSTRAINT DF_Crudas_Fecha DEFAULT (SYSDATETIME()),

    CONSTRAINT PK_OpinionesCrudas PRIMARY KEY (IdFila),
    CONSTRAINT FK_OpinionesCrudas_Lote FOREIGN KEY (IdLote) REFERENCES stg.LoteExtraccion (IdLote)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OpinionesCrudas_Lote')
    CREATE INDEX IX_OpinionesCrudas_Lote ON stg.OpinionesCrudas (IdLote);
GO
