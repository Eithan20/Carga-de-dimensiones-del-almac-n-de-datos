-- Consultas para comprobar el resultado de la extracción (evidencia para el documento).
USE StagingOpiniones;
GO

-- Resumen de cada extracción
SELECT IdLote, Fuente, Canal, Estado, Registros, DuracionMs, Inicio, Fin, Error
FROM stg.LoteExtraccion
ORDER BY IdLote DESC;

-- Registros guardados por canal en el último lote de cada fuente
SELECT l.Fuente, c.Canal, COUNT(*) AS Registros
FROM stg.OpinionesCrudas c
JOIN stg.LoteExtraccion l ON l.IdLote = c.IdLote
WHERE l.IdLote IN (SELECT MAX(IdLote) FROM stg.LoteExtraccion WHERE Estado = 'Completado' GROUP BY Fuente)
GROUP BY l.Fuente, c.Canal;

-- Muestra de datos de cada canal
SELECT TOP 3 * FROM stg.OpinionesCrudas WHERE Canal = 'Encuesta' ORDER BY IdFila DESC;
SELECT TOP 3 * FROM stg.OpinionesCrudas WHERE Canal = 'ResenaWeb' ORDER BY IdFila DESC;
SELECT TOP 3 * FROM stg.OpinionesCrudas WHERE Canal = 'ComentarioSocial' ORDER BY IdFila DESC;
GO
