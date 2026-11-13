namespace ProductCsvImporter.Models;

public sealed class ProductListViewModel
{
    public IReadOnlyList<Product> Products { get; init; } = [];
    public int TotalCount => Products.Count;
    public int ActiveCount => Products.Count(product => product.IsActive);
    public decimal TotalStockValue => Products.Sum(product => product.Price * product.Stock);
}
