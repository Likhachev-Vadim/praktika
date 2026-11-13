using ProductCsvImporter.Models;

namespace ProductCsvImporter.Services;

public interface IProductImportService
{
    Task<ImportResult> ImportAsync(Stream csvStream, CancellationToken cancellationToken = default);
}
