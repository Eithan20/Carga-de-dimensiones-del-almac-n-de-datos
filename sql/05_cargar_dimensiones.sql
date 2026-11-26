-- Carga de las dimensiones del almacén de datos (AnalisisOpinionesAnalitica)
-- Origen: base transaccional AnalisisOpinionesTransaccional (mismo servidor).
-- Se puede ejecutar varias veces: inserta lo nuevo y actualiza lo que cambió, sin duplicar.
-- Requisito: haber ejecutado antes el script del modelo analítico y tener cargada la transaccional.

USE AnalisisOpinionesAnalitica;
GO

-- DimProducto

CREATE OR ALTER PROCEDURE dbo.usp_CargarDimProducto
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @cambios TABLE (Accion NVARCHAR(10));

    BEGIN TRY
        BEGIN TRANSACTION;

        MERGE dbo.DimProducto AS destino
        USING (SELECT IdProducto, Nombre, Categoria
               FROM AnalisisOpinionesTransaccional.dbo.Productos) AS origen
            ON destino.IdProductoOrigen = origen.IdProducto
        WHEN MATCHED AND (destino.Nombre <> origen.Nombre OR destino.Categoria <> origen.Categoria) THEN
            UPDATE SET Nombre = origen.Nombre, Categoria = origen.Categoria
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (IdProductoOrigen, Nombre, Categoria)
            VALUES (origen.IdProducto, origen.Nombre, origen.Categoria)
        OUTPUT $action INTO @cambios;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT 'DimProducto' AS Dimension,
           ISNULL(SUM(CASE WHEN Accion = 'INSERT' THEN 1 ELSE 0 END), 0) AS Insertados,
           ISNULL(SUM(CASE WHEN Accion = 'UPDATE' THEN 1 ELSE 0 END), 0) AS Actualizados,
           (SELECT COUNT(*) FROM dbo.DimProducto) AS TotalEnDimension
    FROM @cambios;
END;
GO

-- DimCliente (la fila 0 "Cliente anónimo" no se toca)
-- La transaccional solo trae Id y Nombre; País, RangoEdad y TipoCliente quedan con su valor por defecto.

CREATE OR ALTER PROCEDURE dbo.usp_CargarDimCliente
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @cambios TABLE (Accion NVARCHAR(10));

    BEGIN TRY
        BEGIN TRANSACTION;

        MERGE dbo.DimCliente AS destino
        USING (SELECT IdCliente, Nombre
               FROM AnalisisOpinionesTransaccional.dbo.Clientes) AS origen
            ON destino.IdClienteOrigen = origen.IdCliente
        WHEN MATCHED AND destino.Nombre <> origen.Nombre THEN
            UPDATE SET Nombre = origen.Nombre
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (IdClienteOrigen, Nombre)
            VALUES (origen.IdCliente, origen.Nombre)
        OUTPUT $action INTO @cambios;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT 'DimCliente' AS Dimension,
           ISNULL(SUM(CASE WHEN Accion = 'INSERT' THEN 1 ELSE 0 END), 0) AS Insertados,
           ISNULL(SUM(CASE WHEN Accion = 'UPDATE' THEN 1 ELSE 0 END), 0) AS Actualizados,
           (SELECT COUNT(*) FROM dbo.DimCliente) AS TotalEnDimension
    FROM @cambios;
END;
GO

-- DimFecha (se genera, no se copia)
-- Sin parámetros: cubre desde el 1 de enero del año de la fecha más antigua
-- hasta el 31 de diciembre del año de la fecha más reciente de las opiniones.

CREATE OR ALTER PROCEDURE dbo.usp_CargarDimFecha
    @FechaInicio DATE = NULL,
    @FechaFin DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaInicio IS NULL OR @FechaFin IS NULL
    BEGIN
        DECLARE @minima DATE, @maxima DATE;

        SELECT @minima = MIN(Fecha), @maxima = MAX(Fecha)
        FROM (
            SELECT Fecha FROM AnalisisOpinionesTransaccional.dbo.Encuestas
            UNION ALL
            SELECT Fecha FROM AnalisisOpinionesTransaccional.dbo.ResenasWeb
            UNION ALL
            SELECT Fecha FROM AnalisisOpinionesTransaccional.dbo.ComentariosSociales
        ) AS f;

        IF @minima IS NULL
        BEGIN
            THROW 50001, 'No hay fechas en la base transaccional para generar DimFecha.', 1;
        END;

        SET @FechaInicio = ISNULL(@FechaInicio, DATEFROMPARTS(YEAR(@minima), 1, 1));
        SET @FechaFin = ISNULL(@FechaFin, DATEFROMPARTS(YEAR(@maxima), 12, 31));
    END;

    DECLARE @antes INT = (SELECT COUNT(*) FROM dbo.DimFecha);

    BEGIN TRY
        BEGIN TRANSACTION;

        ;WITH Dias AS
        (
            SELECT @FechaInicio AS Fecha
            UNION ALL
            SELECT DATEADD(DAY, 1, Fecha) FROM Dias WHERE Fecha < @FechaFin
        )
        INSERT INTO dbo.DimFecha
            (FechaKey, Fecha, Anio, Semestre, Trimestre, Mes, NombreMes, AnioMes, Dia, DiaSemana, NombreDia, Evento)
        SELECT
            YEAR(d.Fecha) * 10000 + MONTH(d.Fecha) * 100 + DAY(d.Fecha),
            d.Fecha,
            YEAR(d.Fecha),
            CASE WHEN MONTH(d.Fecha) <= 6 THEN 1 ELSE 2 END,
            DATEPART(QUARTER, d.Fecha),
            MONTH(d.Fecha),
            CHOOSE(MONTH(d.Fecha), 'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
                   'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'),
            CONVERT(CHAR(7), d.Fecha, 126),
            DAY(d.Fecha),
            (DATEDIFF(DAY, '19000101', d.Fecha) % 7) + 1,   -- 1 = lunes ... 7 = domingo
            CHOOSE((DATEDIFF(DAY, '19000101', d.Fecha) % 7) + 1,
                   'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'),
            NULL
        FROM Dias AS d
        WHERE NOT EXISTS (SELECT 1 FROM dbo.DimFecha x WHERE x.Fecha = d.Fecha)
        OPTION (MAXRECURSION 0);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT 'DimFecha' AS Dimension,
           (SELECT COUNT(*) FROM dbo.DimFecha) - @antes AS Insertados,
           0 AS Actualizados,
           (SELECT COUNT(*) FROM dbo.DimFecha) AS TotalEnDimension;
END;
GO

-- DimFuente (valores fijos del modelo; el CHECK de la tabla limita los canales permitidos)

CREATE OR ALTER PROCEDURE dbo.usp_CargarDimFuente
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @cambios TABLE (Accion NVARCHAR(10));

    BEGIN TRY
        BEGIN TRANSACTION;

        MERGE dbo.DimFuente AS destino
        USING (VALUES
                ('Encuesta', 'EncuestaInterna'),
                ('ResenaWeb', 'SitioWeb'),
                ('ComentarioSocial', 'Instagram'),
                ('ComentarioSocial', 'Facebook'),
                ('ComentarioSocial', 'Twitter')
              ) AS origen (Canal, Plataforma)
            ON destino.Canal = origen.Canal AND destino.Plataforma = origen.Plataforma
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (Canal, Plataforma) VALUES (origen.Canal, origen.Plataforma)
        OUTPUT $action INTO @cambios;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT 'DimFuente' AS Dimension,
           ISNULL(SUM(CASE WHEN Accion = 'INSERT' THEN 1 ELSE 0 END), 0) AS Insertados,
           0 AS Actualizados,
           (SELECT COUNT(*) FROM dbo.DimFuente) AS TotalEnDimension
    FROM @cambios;
END;
GO

-- DimClasificacion (valores fijos del modelo)

CREATE OR ALTER PROCEDURE dbo.usp_CargarDimClasificacion
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @cambios TABLE (Accion NVARCHAR(10));

    BEGIN TRY
        BEGIN TRANSACTION;

        MERGE dbo.DimClasificacion AS destino
        USING (VALUES
                ('Positiva', 'Puntaje 4 o 5'),
                ('Neutra', 'Puntaje 3'),
                ('Negativa', 'Puntaje 1 o 2'),
                ('Sin clasificar', 'Opinión sin puntaje (por ejemplo, redes sociales)')
              ) AS origen (Clasificacion, Criterio)
            ON destino.Clasificacion = origen.Clasificacion
        WHEN MATCHED AND destino.Criterio <> origen.Criterio THEN
            UPDATE SET Criterio = origen.Criterio
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (Clasificacion, Criterio) VALUES (origen.Clasificacion, origen.Criterio)
        OUTPUT $action INTO @cambios;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT 'DimClasificacion' AS Dimension,
           ISNULL(SUM(CASE WHEN Accion = 'INSERT' THEN 1 ELSE 0 END), 0) AS Insertados,
           ISNULL(SUM(CASE WHEN Accion = 'UPDATE' THEN 1 ELSE 0 END), 0) AS Actualizados,
           (SELECT COUNT(*) FROM dbo.DimClasificacion) AS TotalEnDimension
    FROM @cambios;
END;
GO

-- Procedimiento principal: carga las cinco dimensiones (útil para ejecutar todo desde SSMS)

CREATE OR ALTER PROCEDURE dbo.usp_CargarDimensiones
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @resumen TABLE
    (
        Dimension VARCHAR(30), Insertados INT, Actualizados INT, TotalEnDimension INT
    );

    INSERT INTO @resumen EXEC dbo.usp_CargarDimFecha;
    INSERT INTO @resumen EXEC dbo.usp_CargarDimProducto;
    INSERT INTO @resumen EXEC dbo.usp_CargarDimCliente;
    INSERT INTO @resumen EXEC dbo.usp_CargarDimFuente;
    INSERT INTO @resumen EXEC dbo.usp_CargarDimClasificacion;

    SELECT Dimension, Insertados, Actualizados, TotalEnDimension FROM @resumen;
END;
GO

-- Este script solo CREA los procedimientos. La carga la ejecuta el servicio en C# (ADO.NET).
-- Para probar un procedimiento a mano desde SSMS: EXEC dbo.usp_CargarDimensiones;
