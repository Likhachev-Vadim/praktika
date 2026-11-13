using ProductCsvImporter.Models;

namespace ProductCsvImporter.Services;

public interface IProductRepository
{
    IReadOnlyList<Product> GetAll();
    bool TryAdd(Product product);
}
