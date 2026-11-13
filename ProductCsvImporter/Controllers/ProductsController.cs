using Microsoft.AspNetCore.Mvc;
using ProductCsvImporter.Models;
using ProductCsvImporter.Services;

namespace ProductCsvImporter.Controllers;

public sealed class ProductsController(IProductRepository repository, IProductImportService importService) : Controller
{
    private const long MaxFileLength = 2 * 1024 * 1024;

    [HttpGet]
    public IActionResult Index() => View(new ProductListViewModel { Products = repository.GetAll() });

    [HttpGet]
    public IActionResult Import() => View(new ImportPageViewModel());

    [HttpGet]
    public IActionResult Error() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(ImportPageViewModel model, CancellationToken cancellationToken)
    {
        if (model.File is null)
        {
            model.Message = "Выберите CSV-файл для импорта.";
            return View(model);
        }

        if (!string.Equals(Path.GetExtension(model.File.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            model.Message = "Поддерживаются только файлы с расширением .csv.";
            return View(model);
        }

        if (model.File.Length > MaxFileLength)
        {
            model.Message = "Размер файла не должен превышать 2 МБ.";
            return View(model);
        }

        await using var stream = model.File.OpenReadStream();
        model.Result = await importService.ImportAsync(stream, cancellationToken);
        model.File = null;
        return View(model);
    }
}
