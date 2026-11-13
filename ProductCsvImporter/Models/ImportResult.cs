namespace ProductCsvImporter.Models;

public sealed class ImportResult
{
    public int TotalRows { get; init; }
    public int ImportedCount { get; init; }
    public IReadOnlyList<ImportError> Errors { get; init; } = [];
    public int ErrorCount => Errors.Count;
}
