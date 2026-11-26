using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Domain.Entities;
using OpinionesETL.Domain.Interfaces;
using OpinionesETL.Infrastructure.Configuration;

namespace OpinionesETL.Infrastructure.Extractors;

/// <summary>Extrae las encuestas internas desde un archivo CSV usando CsvHelper.</summary>
public sealed class CsvExtractor : IExtractor
{
    // Columnas que el archivo debe traer sí o sí (sin tildes y en minúscula, como las compara Normalize).
    private static readonly string[] RequiredColumns =
        ["idopinion", "idcliente", "idproducto", "fecha", "comentario"];

    private readonly CsvSourceOptions _options;
    private readonly ILogger<CsvExtractor> _logger;

    public CsvExtractor(IOptions<CsvSourceOptions> options, ILogger<CsvExtractor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string SourceName => "CSV";

    public string Canal => _options.Canal;

    public async IAsyncEnumerable<RawOpinion> ExtractAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var path = Path.IsPathRooted(_options.Path)
            ? _options.Path
            : Path.Combine(AppContext.BaseDirectory, _options.Path);

        if (!File.Exists(path))
            throw new FileNotFoundException($"No se encontró el archivo CSV: {path}", path);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = _options.Delimiter,
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null,
            // "Clasificación" y "Clasificacion" se tratan como la misma columna.
            PrepareHeaderForMatch = args => Normalize(args.Header)
        };

        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, config);

        if (!await csv.ReadAsync() || !csv.ReadHeader())
            throw new InvalidDataException($"El archivo CSV está vacío: {path}");

        var headers = csv.HeaderRecord!.Select(Normalize).ToHashSet();
        var missing = RequiredColumns.Where(c => !headers.Contains(c)).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException(
                $"Al CSV le faltan columnas obligatorias: {string.Join(", ", missing)}");

        var fileName = Path.GetFileName(path);
        long rows = 0;

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows++;

            yield return new RawOpinion(
                Canal: _options.Canal,
                Origen: fileName,
                IdOrigen: Field(csv, "IdOpinion"),
                IdCliente: Field(csv, "IdCliente"),
                IdProducto: Field(csv, "IdProducto"),
                Fecha: Field(csv, "Fecha"),
                Comentario: Field(csv, "Comentario"),
                Clasificacion: Field(csv, "Clasificacion"),
                Puntaje: Field(csv, "PuntajeSatisfaccion"),
                Plataforma: Field(csv, "Fuente"));
        }

        _logger.LogDebug("[CSV] {Rows} filas leídas de {File}", rows, fileName);
    }

    private static string? Field(CsvReader csv, string name)
    {
        if (!csv.TryGetField(name, out string? value)) return null;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>Quita tildes, espacios y mayúsculas para comparar nombres de columna.</summary>
    private static string Normalize(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return string.Empty;

        var decomposed = header.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().ToLowerInvariant();
    }
}
