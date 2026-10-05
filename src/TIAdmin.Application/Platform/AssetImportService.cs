namespace TIAdmin.Application.Platform;

using System.Globalization;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Importacion de activos desde CSV/Excel (migracion de inventario, SPECS.md seccion 46).
/// Todo o nada (ADR-037): se validan todas las filas contra la base y contra el propio archivo y,
/// si hay errores, no se importa ninguna. Con <c>dryRun</c> solo se valida.
/// </summary>
public interface IAssetImportService
{
    Task<ImportResult> ImportAsync(Stream content, string format, bool dryRun, CancellationToken cancellationToken = default);

    TabularData Template();
}

public sealed class AssetImportService(IUnitOfWork unitOfWork, ITabularFileReader reader) : IAssetImportService
{
    public const int MaxRows = 10_000;

    private static readonly string[] Columns =
    [
        "AssetCode", "Name", "AssetType", "SerialNumber", "Brand", "Model", "PurchaseDate", "PurchaseCost",
        "WarrantyExpiration", "Location", "Department", "Notes"
    ];

    private static readonly HashSet<string> Required = new(StringComparer.OrdinalIgnoreCase) { "AssetCode", "Name", "AssetType" };

    public TabularData Template() => new("Activos", Columns,
    [
        ["LT-0001", "Laptop Dell Latitude 5440", "LAPTOP", "SN123456", "Dell", "Latitude 5440", "2026-01-15", 1250.00m,
            "2029-01-15", "", "", "Ejemplo: borrar esta fila"]
    ]);

    public async Task<ImportResult> ImportAsync(Stream content, string format, bool dryRun, CancellationToken cancellationToken = default)
    {
        var table = await reader.ReadAsync(content, format, cancellationToken);
        var index = table.Headers
            .Select((header, position) => (header, position))
            .Where(h => h.header.Length > 0)
            .GroupBy(h => h.header, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().position, StringComparer.OrdinalIgnoreCase);

        var missing = Required.Where(c => !index.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            throw new DomainValidationException("IMPORT_MISSING_COLUMNS",
                $"Faltan columnas obligatorias: {string.Join(", ", missing)}. Descargue la plantilla.");
        }

        if (table.Rows.Count > MaxRows)
        {
            throw new DomainValidationException("IMPORT_TOO_MANY_ROWS", $"El archivo excede {MaxRows} filas; divida la importacion.");
        }

        string Cell(IReadOnlyList<object?> row, string column) =>
            index.TryGetValue(column, out var position) && position < row.Count ? row[position]?.ToString()?.Trim() ?? string.Empty : string.Empty;

        // Catalogos y existentes en consultas masivas, no por fila.
        var types = (await unitOfWork.AssetTypes.ListAsync(true, cancellationToken)).ToDictionary(t => t.Code, t => t.Id, StringComparer.OrdinalIgnoreCase);
        var locations = (await unitOfWork.Locations.GetActiveAsync(cancellationToken)).ToDictionary(l => l.Code, l => l.Id, StringComparer.OrdinalIgnoreCase);
        var departments = (await unitOfWork.Departments.GetActiveAsync(cancellationToken)).ToDictionary(d => d.Code, d => d.Id, StringComparer.OrdinalIgnoreCase);
        var codes = table.Rows.Select(r => Cell(r, "AssetCode").ToUpperInvariant()).Where(c => c.Length > 0).ToList();
        var serials = table.Rows.Select(r => Cell(r, "SerialNumber")).Where(s => s.Length > 0).ToList();
        var existingCodes = await unitOfWork.Assets.GetExistingAssetCodesAsync(codes, cancellationToken);
        var existingSerials = await unitOfWork.Assets.GetExistingSerialNumbersAsync(serials, cancellationToken);

        var errors = new List<ImportError>();
        var assets = new List<Asset>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            var rowNumber = i + 2; // fila 1 = encabezados
            var rowErrors = new List<ImportError>();
            void Error(string field, string message) => rowErrors.Add(new ImportError(rowNumber, field, message));

            var code = Cell(row, "AssetCode").ToUpperInvariant();
            var name = Cell(row, "Name");
            var typeCode = Cell(row, "AssetType");
            var serial = Cell(row, "SerialNumber");

            if (code.Length == 0)
            {
                Error("AssetCode", "Obligatorio.");
            }
            else if (code.Length > 30)
            {
                Error("AssetCode", "Maximo 30 caracteres.");
            }
            else if (existingCodes.Contains(code))
            {
                Error("AssetCode", $"El codigo {code} ya existe.");
            }
            else if (!seenCodes.Add(code))
            {
                Error("AssetCode", $"El codigo {code} esta repetido en el archivo.");
            }

            if (name.Length == 0)
            {
                Error("Name", "Obligatorio.");
            }
            else if (name.Length > 150)
            {
                Error("Name", "Maximo 150 caracteres.");
            }

            if (!types.TryGetValue(typeCode, out var typeId))
            {
                Error("AssetType", $"Tipo de activo '{typeCode}' inexistente o inactivo (use el codigo, p. ej. LAPTOP).");
            }

            if (serial.Length > 0)
            {
                if (existingSerials.Contains(serial))
                {
                    Error("SerialNumber", $"La serie {serial} ya esta registrada.");
                }
                else if (!seenSerials.Add(serial))
                {
                    Error("SerialNumber", $"La serie {serial} esta repetida en el archivo.");
                }
            }

            var purchaseDate = ParseDate(Cell(row, "PurchaseDate"), "PurchaseDate", Error);
            var warranty = ParseDate(Cell(row, "WarrantyExpiration"), "WarrantyExpiration", Error);
            if (purchaseDate is { } p && warranty is { } w && w < p)
            {
                Error("WarrantyExpiration", "La garantia no puede vencer antes de la compra.");
            }

            decimal? cost = null;
            var costText = Cell(row, "PurchaseCost");
            if (costText.Length > 0)
            {
                if (decimal.TryParse(costText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
                {
                    cost = parsed;
                }
                else
                {
                    Error("PurchaseCost", "Debe ser un numero mayor o igual a cero (punto decimal).");
                }
            }

            int? locationId = Lookup(Cell(row, "Location"), locations, "Location", "Ubicacion", Error);
            int? departmentId = Lookup(Cell(row, "Department"), departments, "Department", "Departamento", Error);

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                continue;
            }

            assets.Add(new Asset
            {
                AssetCode = code,
                Name = name,
                AssetTypeId = typeId,
                SerialNumber = serial.Length > 0 ? serial : null,
                Brand = Optional(Cell(row, "Brand"), 100),
                Model = Optional(Cell(row, "Model"), 100),
                PurchaseDate = purchaseDate,
                PurchaseCost = cost,
                WarrantyExpiration = warranty,
                LocationId = locationId,
                DepartmentId = departmentId,
                Notes = Optional(Cell(row, "Notes"), 1000)
            });
        }

        if (errors.Count > 0 || dryRun)
        {
            return new ImportResult(table.Rows.Count, 0, dryRun, errors);
        }

        foreach (var asset in assets)
        {
            await unitOfWork.Assets.AddAsync(asset, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ImportResult(table.Rows.Count, assets.Count, false, []);
    }

    private static DateOnly? ParseDate(string value, string field, Action<string, string> error)
    {
        if (value.Length == 0)
        {
            return null;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        // Excel puede entregar la fecha con hora (celda de tipo fecha).
        if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            return DateOnly.FromDateTime(dateTime);
        }

        error(field, "Fecha invalida; use AAAA-MM-DD.");
        return null;
    }

    private static int? Lookup(string code, Dictionary<string, int> catalog, string field, string label, Action<string, string> error)
    {
        if (code.Length == 0)
        {
            return null;
        }

        if (catalog.TryGetValue(code, out var id))
        {
            return id;
        }

        error(field, $"{label} '{code}' inexistente o inactiva (use el codigo).");
        return null;
    }

    private static string? Optional(string value, int max) =>
        value.Length == 0 ? null : value.Length > max ? value[..max] : value;
}
