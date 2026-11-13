using Microsoft.AspNetCore.Http;

namespace ProductCsvImporter.Models;

public sealed class ImportPageViewModel
{
    public IFormFile? File { get; set; }
    public ImportResult? Result { get; set; }
    public string? Message { get; set; }
}
