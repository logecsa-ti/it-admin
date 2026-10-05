namespace TIAdmin.Infrastructure.Services;

using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Escritura/lectura de CSV (UTF-8 con BOM, separador coma) y Excel (.xlsx, ClosedXML).
/// Los textos que empiezan con = + - @ se neutralizan con un apostrofo (inyeccion de formulas).
/// </summary>
public sealed class TabularFiles : ITabularFileWriter, ITabularFileReader
{
    public bool Supports(string format) => format is "csv" or "xlsx";

    public async Task WriteAsync(TabularData data, string format, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(output);

        switch (format)
        {
            case "csv":
                await WriteCsvAsync(data, output, cancellationToken);
                break;
            case "xlsx":
                WriteXlsx(data, output);
                break;
            default:
                throw new DomainValidationException("EXPORT_FORMAT_NOT_SUPPORTED", $"Formato no soportado: {format}. Use csv o xlsx.");
        }
    }

    public async Task<TabularData> ReadAsync(Stream input, string format, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var rows = format switch
        {
            "csv" => await ReadCsvAsync(input, cancellationToken),
            "xlsx" => ReadXlsx(input),
            _ => throw new DomainValidationException("IMPORT_FORMAT_NOT_SUPPORTED", $"Formato no soportado: {format}. Use csv o xlsx.")
        };

        if (rows.Count == 0)
        {
            throw new DomainValidationException("IMPORT_EMPTY", "El archivo no tiene encabezados.");
        }

        var headers = rows[0].Select(h => h?.ToString()?.Trim() ?? string.Empty).ToList();
        var dataRows = rows.Skip(1)
            .Where(r => r.Any(v => !string.IsNullOrWhiteSpace(v?.ToString())))
            .Select(r => (IReadOnlyList<object?>)r)
            .ToList();
        return new TabularData("import", headers, dataRows);
    }

    private static async Task WriteCsvAsync(TabularData data, Stream output, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        await writer.WriteLineAsync(string.Join(",", data.Headers.Select(EscapeCsv)));
        foreach (var row in data.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(",", row.Select(v => EscapeCsv(FormatValue(v)))));
        }
    }

    private static void WriteXlsx(TabularData data, Stream output)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName(data.Title));

        for (var c = 0; c < data.Headers.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = data.Headers[c];
        }

        sheet.Row(1).Style.Font.Bold = true;

        for (var r = 0; r < data.Rows.Count; r++)
        {
            var row = data.Rows[r];
            for (var c = 0; c < row.Count; c++)
            {
                var cell = sheet.Cell(r + 2, c + 1);
                switch (row[c])
                {
                    case null:
                        break;
                    case int or long or decimal or double:
                        cell.Value = Convert.ToDouble(row[c], CultureInfo.InvariantCulture);
                        break;
                    case DateTime dateTime:
                        cell.Value = dateTime;
                        cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                        break;
                    case DateOnly date:
                        cell.Value = date.ToDateTime(TimeOnly.MinValue);
                        cell.Style.DateFormat.Format = "yyyy-mm-dd";
                        break;
                    case bool flag:
                        cell.Value = flag ? "Si" : "No";
                        break;
                    default:
                        cell.Value = Neutralize(row[c]!.ToString()!);
                        break;
                }
            }
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(1, Math.Min(data.Rows.Count + 1, 200));
        workbook.SaveAs(output);
    }

    private static async Task<List<List<object?>>> ReadCsvAsync(Stream input, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken);

        var rows = new List<List<object?>>();
        var row = new List<object?>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (ch == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',' or ';':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = [];
                    break;
                default:
                    field.Append(ch);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private static List<List<object?>> ReadXlsx(Stream input)
    {
        using var workbook = new XLWorkbook(input);
        var sheet = workbook.Worksheets.First();
        var range = sheet.RangeUsed();
        if (range is null)
        {
            return [];
        }

        var rows = new List<List<object?>>();
        foreach (var xlRow in range.Rows())
        {
            rows.Add(xlRow.Cells().Select(cell => (object?)CellText(cell)).ToList());
        }

        return rows;
    }

    private static string CellText(IXLCell cell) => cell.DataType switch
    {
        XLDataType.DateTime => cell.GetDateTime().ToString(cell.GetDateTime().TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        XLDataType.Number => cell.GetDouble().ToString(CultureInfo.InvariantCulture),
        XLDataType.Blank => string.Empty,
        _ => cell.GetFormattedString().Trim()
    };

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool flag => flag ? "Si" : "No",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => Neutralize(value.ToString() ?? string.Empty)
    };

    private static string EscapeCsv(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r', ';']) >= 0 ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;

    private static string Neutralize(string value) =>
        value.Length > 0 && value[0] is '=' or '+' or '-' or '@' ? $"'{value}" : value;

    private static string SheetName(string title)
    {
        var clean = new string(title.Where(c => c is not ('\\' or '/' or '?' or '*' or '[' or ']' or ':')).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "Datos" : clean[..Math.Min(clean.Length, 31)];
    }
}
