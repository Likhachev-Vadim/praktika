using System.Globalization;
using System.Text;
using ProductCsvImporter.Models;

namespace ProductCsvImporter.Services;

public sealed class ProductImportService(IProductRepository repository) : IProductImportService
{
    private static readonly string[] RequiredHeaders = ["sku", "name", "price", "stock", "brand", "warranty_months"];

    public async Task<ImportResult> ImportAsync(Stream csvStream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return new ImportResult { Errors = [new ImportError(1, "CSV-файл пуст или не содержит заголовки.")] };
        }

        var delimiter = DetectDelimiter(headerLine);
        var parsedHeaders = ParseRow(headerLine, delimiter)
            .Select((name, index) => (Name: name.Trim().TrimStart('\uFEFF').ToLowerInvariant(), Index: index))
            .Where(item => item.Name.Length > 0)
            .ToArray();
        var duplicateHeaders = parsedHeaders.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicateHeaders.Length > 0)
        {
            return new ImportResult
            {
                Errors = [new ImportError(1, $"В заголовке повторяются поля: {string.Join(", ", duplicateHeaders)}.")]
            };
        }

        var headers = parsedHeaders.ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);

        var missingHeaders = RequiredHeaders.Where(header => !headers.ContainsKey(header)).ToArray();
        if (missingHeaders.Length > 0)
        {
            return new ImportResult
            {
                Errors = [new ImportError(1, $"В заголовке отсутствуют обязательные поля: {string.Join(", ", missingHeaders)}.")]
            };
        }

        var errors = new List<ImportError>();
        var totalRows = 0;
        var importedCount = 0;
        var physicalLine = 1;
        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            physicalLine++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            totalRows++;
            var values = ParseRow(line, delimiter);
            var rowError = TryMapProduct(values, headers, physicalLine, out var product);
            if (rowError is not null)
            {
                errors.Add(rowError);
                continue;
            }

            if (!repository.TryAdd(product!))
            {
                errors.Add(new ImportError(physicalLine, $"Артикул «{product!.Sku}» уже есть в списке товаров."));
                continue;
            }

            importedCount++;
        }

        if (totalRows == 0)
        {
            errors.Add(new ImportError(2, "В CSV-файле нет строк с товарами."));
        }

        return new ImportResult
        {
            TotalRows = totalRows,
            ImportedCount = importedCount,
            Errors = errors
        };
    }

    private static ImportError? TryMapProduct(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> headers,
        int rowNumber,
        out Product? product)
    {
        product = null;
        string Get(string name, string fallback = "") =>
            headers.TryGetValue(name, out var index) && index < values.Count ? values[index].Trim() : fallback;

        var sku = Get("sku");
        var name = Get("name");
        if (sku.Length == 0) return new ImportError(rowNumber, "Не указан артикул (sku).");
        if (name.Length == 0) return new ImportError(rowNumber, "Не указано название товара (name).");

        if (!decimal.TryParse(Get("price"), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price <= 0)
        {
            return new ImportError(rowNumber, "Цена должна быть числом больше нуля.");
        }

        if (!int.TryParse(Get("stock"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stock) || stock < 0)
        {
            return new ImportError(rowNumber, "Остаток должен быть целым неотрицательным числом.");
        }

        var activeText = Get("is_active", "true");
        if (activeText.Length == 0) activeText = "true";
        bool isActive;
        if (activeText == "1") isActive = true;
        else if (activeText == "0") isActive = false;
        else if (!bool.TryParse(activeText, out isActive))
        {
            return new ImportError(rowNumber, "Поле is_active должно содержать true, false, 1 или 0.");
        }

        var brand = Get("brand");
        if (brand.Length == 0)
        {
            return new ImportError(rowNumber, "Не указан бренд товара (brand).");
        }

        if (!int.TryParse(Get("warranty_months"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var warrantyMonths)
            || warrantyMonths is < 0 or > 60)
        {
            return new ImportError(rowNumber, "Гарантия (warranty_months) должна быть целым числом от 0 до 60 месяцев.");
        }

        var currency = Get("currency", "RUB");
        var category = Get("category_name", "Без категории");
        product = new Product(
            sku,
            name,
            Get("description"),
            price,
            currency.Length == 0 ? "RUB" : currency,
            category.Length == 0 ? "Без категории" : category,
            stock,
            Get("image_url"),
            isActive,
            brand,
            warrantyMonths,
            Get("model_number"));

        return null;
    }

    private static char DetectDelimiter(string header)
    {
        var commaCount = CountUnquoted(header, ',');
        var semicolonCount = CountUnquoted(header, ';');
        return semicolonCount > commaCount ? ';' : ',';
    }

    private static int CountUnquoted(string text, char delimiter)
    {
        var quoted = false;
        var count = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '"')
            {
                if (quoted && index + 1 < text.Length && text[index + 1] == '"') index++;
                else quoted = !quoted;
            }
            else if (!quoted && text[index] == delimiter)
            {
                count++;
            }
        }
        return count;
    }

    private static IReadOnlyList<string> ParseRow(string row, char delimiter)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < row.Length; index++)
        {
            var character = row[index];
            if (character == '"')
            {
                if (quoted && index + 1 < row.Length && row[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == delimiter && !quoted)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }

        values.Add(value.ToString());
        return values;
    }
}
