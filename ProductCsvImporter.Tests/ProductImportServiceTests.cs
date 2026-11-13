using System.Text;
using ProductCsvImporter.Services;

namespace ProductCsvImporter.Tests;

public sealed class ProductImportServiceTests
{
    [Fact]
    public async Task ImportsValidRowsAndKeepsQuotedComma()
    {
        var (service, repository) = CreateService();
        var csv = "sku,name,description,price,currency,category_name,stock,image_url,is_active,brand,warranty_months,model_number\n" +
                  "1001,Товар 1,\"Описание, с запятой\",1200.50,RUB,Категория,3,,true,Бренд,60,М-1\n" +
                  "1002,Товар 2,Описание,900,RUB,Категория,1,,false,Бренд,0,";

        var result = await Import(service, csv);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Empty(result.Errors);
        var first = Assert.Single(repository.GetAll(), item => item.Sku == "1001");
        Assert.Equal("Описание, с запятой", first.Description);
        Assert.Equal("Бренд", first.Brand);
        Assert.Equal(60, first.WarrantyMonths);
        Assert.Equal("М-1", first.ModelNumber);
        var second = Assert.Single(repository.GetAll(), item => item.Sku == "1002");
        Assert.Equal(0, second.WarrantyMonths);
        Assert.Empty(second.ModelNumber);
    }

    [Fact]
    public async Task AcceptsSemicolonSeparatedCyrillicCsv()
    {
        var (service, repository) = CreateService();
        var csv = "sku;name;price;stock;category_name;brand;warranty_months\n1001;Клавиатура;3490.50;4;Периферия;Бренд;12";

        var result = await Import(service, csv);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal("Клавиатура", Assert.Single(repository.GetAll()).Name);
    }

    [Fact]
    public async Task AcceptsNumericActivityValues()
    {
        var (service, repository) = CreateService();
        var csv = "sku,name,price,stock,is_active,brand,warranty_months\n1001,Активный,100,1,1,Бренд,12\n1002,Неактивный,200,2,0,Бренд,12";

        var result = await Import(service, csv);

        Assert.Equal(2, result.ImportedCount);
        Assert.True(Assert.Single(repository.GetAll(), item => item.Sku == "1001").IsActive);
        Assert.False(Assert.Single(repository.GetAll(), item => item.Sku == "1002").IsActive);
    }

    [Fact]
    public async Task ReportsInvalidPriceAndNegativeStockWithLineNumbers()
    {
        var (service, _) = CreateService();
        var csv = "sku,name,price,stock,brand,warranty_months\n1001,Товар,не число,2,Бренд,12\n1002,Другой,500,-1,Бренд,12";

        var result = await Import(service, csv);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(new[] { 2, 3 }, result.Errors.Select(error => error.RowNumber));
    }

    [Fact]
    public async Task RejectsDuplicateSkuInsideOneFile()
    {
        var (service, repository) = CreateService();
        var csv = "sku,name,price,stock,brand,warranty_months\n1001,Первый,100,1,Бренд,12\n1001,Дубликат,200,2,Бренд,12";

        var result = await Import(service, csv);

        Assert.Equal(1, result.ImportedCount);
        Assert.Single(repository.GetAll());
        Assert.Equal(3, Assert.Single(result.Errors).RowNumber);
    }

    [Fact]
    public async Task ReportsEmptyFileAndMissingHeaders()
    {
        var (service, _) = CreateService();
        var empty = await Import(service, "");
        var missing = await Import(service, "sku,name\n1001,Товар");

        Assert.Equal(1, empty.Errors[0].RowNumber);
        Assert.Contains("price", missing.Errors[0].Message);
        Assert.Contains("stock", missing.Errors[0].Message);
        Assert.Contains("brand", missing.Errors[0].Message);
        Assert.Contains("warranty_months", missing.Errors[0].Message);
    }

    [Fact]
    public async Task RejectsDuplicateColumnNamesInsteadOfCrashing()
    {
        var (service, _) = CreateService();
        var result = await Import(service, "sku,name,price,stock,price\n1,Товар,2,3,4");

        Assert.Contains("повторяются", result.Errors[0].Message);
    }

    [Theory]
    [InlineData(" ", "12", "brand")]
    [InlineData("Бренд", "-1", "warranty_months")]
    [InlineData("Бренд", "61", "warranty_months")]
    [InlineData("Бренд", "12.5", "warranty_months")]
    [InlineData("Бренд", "", "warranty_months")]
    public async Task RejectsInvalidElectronicsFieldsButImportsNextRow(string brand, string warranty, string field)
    {
        var (service, repository) = CreateService();
        var csv = "sku,name,price,stock,brand,warranty_months\n" +
                  $"1001,Ошибка,100,1,{brand},{warranty}\n" +
                  "1002,Исправный,100,1,Бренд,12";

        var result = await Import(service, csv);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.RowNumber);
        Assert.Contains(field, error.Message);
        Assert.Equal("1002", Assert.Single(repository.GetAll()).Sku);
    }

    private static (ProductImportService Service, InMemoryProductRepository Repository) CreateService()
    {
        var repository = new InMemoryProductRepository();
        return (new ProductImportService(repository), repository);
    }

    private static async Task<ProductCsvImporter.Models.ImportResult> Import(ProductImportService service, string csv)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await service.ImportAsync(stream);
    }
}
