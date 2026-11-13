using System.Collections.Concurrent;
using ProductCsvImporter.Models;

namespace ProductCsvImporter.Services;

public sealed class InMemoryProductRepository : IProductRepository
{
    private readonly ConcurrentDictionary<string, Product> _products =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Product> GetAll() => _products.Values
        .OrderBy(product => product.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    public bool TryAdd(Product product) => _products.TryAdd(product.Sku, product);
}
