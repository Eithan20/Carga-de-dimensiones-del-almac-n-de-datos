-- Verificación de la carga de dimensiones (AnalisisOpinionesAnalitica)
-- Ejecutar después de 05_cargar_dimensiones.sql. Cada consulta es una captura.

USE AnalisisOpinionesAnalitica;
GO

-- 1. Conteo por dimensión
SELECT 'DimFecha' AS Tabla, COUNT(*) AS Filas FROM dbo.DimFecha
UNION ALL SELECT 'DimProducto', COUNT(*) FROM dbo.DimProducto
UNION ALL SELECT 'DimCliente', COUNT(*) FROM dbo.DimCliente
UNION ALL SELECT 'DimFuente', COUNT(*) FROM dbo.DimFuente
UNION ALL SELECT 'DimClasificacion', COUNT(*) FROM dbo.DimClasificacion;

-- 2. Cuadre contra la transaccional (las diferencias deben ser 0)
SELECT 'Productos' AS Entidad,
       (SELECT COUNT(*) FROM AnalisisOpinionesTransaccional.dbo.Productos) AS EnTransaccional,
       (SELECT COUNT(*) FROM dbo.DimProducto) AS EnDimension
UNION ALL
SELECT 'Clientes (sin el anónimo)',
       (SELECT COUNT(*) FROM AnalisisOpinionesTransaccional.dbo.Clientes),
       (SELECT COUNT(*) FROM dbo.DimCliente WHERE ClienteKey <> 0);

-- 3. Muestras
SELECT TOP 10 * FROM dbo.DimFecha ORDER BY FechaKey;
SELECT TOP 10 * FROM dbo.DimProducto ORDER BY ProductoKey;
SELECT TOP 10 * FROM dbo.DimCliente ORDER BY ClienteKey;
SELECT * FROM dbo.DimFuente;
SELECT * FROM dbo.DimClasificacion;

-- 4. Rango de fechas cubierto contra las fechas de las opiniones
SELECT MIN(Fecha) AS PrimeraFecha, MAX(Fecha) AS UltimaFecha, COUNT(*) AS Dias FROM dbo.DimFecha;
GO
