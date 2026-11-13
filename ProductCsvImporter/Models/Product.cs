namespace ProductCsvImporter.Models;

public sealed record Product(
    string Sku,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    string CategoryName,
    int Stock,
    string ImageUrl,
    bool IsActive,
    string Brand,
    int WarrantyMonths,
    string ModelNumber);
