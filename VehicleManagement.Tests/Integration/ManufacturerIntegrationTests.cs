using System.Net;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VehicleManagement.Data;
using VehicleManagement.Models;

namespace VehicleManagement.Tests.Integration;

public class ManufacturerIntegrationTests : IntegrationTestBase
{
    private const int NonExistingId = 999999;

    private ApplicationDbContext Db =>
        Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    #region Helpers

    private HttpClient CreateNoRedirectClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private async Task<(HttpResponseMessage Response, string Html)> GetPageAsync(string url)
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();

        return (response, html);
    }

    private async Task<HttpStatusCode> GetStatusAsync(string url)
    {
        var (response, _) = await GetPageAsync(url);

        return response.StatusCode;
    }

    private async Task<Manufacturer> AddManufacturerAsync(string name)
    {
        var manufacturer = new Manufacturer
        {
            Name = name,
            IsDefault = false,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Db.Manufacturers.Add(manufacturer);
        await Db.SaveChangesAsync();

        return manufacturer;
    }

    private Task<Manufacturer> GetManufacturerByIdAsync(int id) =>
        Db.Manufacturers
            .AsNoTracking()
            .FirstAsync(m => m.Id == id);

    private static async Task<string> GetAntiForgeryTokenAsync(
        HttpClient client,
        string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(html);
        var token = document
            .QuerySelector("input[name='__RequestVerificationToken']")
            ?.GetAttribute("value");
        Assert.False(
            string.IsNullOrWhiteSpace(token),
            $"{url} did not contain an anti-forgery token.");

        return token!;
    }

    private async Task<HttpResponseMessage> PostFormAsync(
        string tokenUrl,
        string postUrl,
        Dictionary<string, string> fields)
    {
        using var client = CreateNoRedirectClient();

        fields["__RequestVerificationToken"] =
            await GetAntiForgeryTokenAsync(client, tokenUrl);

        using var form = new FormUrlEncodedContent(fields);

        return await client.PostAsync(postUrl, form);
    }

    private Task<HttpResponseMessage> PostDeleteAsync(int id) =>
        PostFormAsync(
            "/Manufacturers/Index",
            "/Manufacturers/Delete",
            new Dictionary<string, string>
            {
                ["id"] = id.ToString()
            });

    #endregion

    #region GET Pages

    [Theory]
    [InlineData("/Manufacturers/Index")]
    [InlineData("/Manufacturers/Create")]
    public async Task Manufacturer_Page_Returns_Success(string url)
    {
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(url));
    }

    [Fact]
    public async Task Manufacturer_Details_WhenManufacturerExists_Returns_Success()
    {
        var manufacturer = await Db.Manufacturers
            .AsNoTracking()
            .FirstAsync(m => !m.IsDeleted);

        Assert.Equal(
            HttpStatusCode.OK,
            await GetStatusAsync($"/Manufacturers/Details/{manufacturer.Id}"));
    }

    [Fact]
    public async Task Manufacturer_Edit_WhenManufacturerExists_Returns_Success()
    {
        var manufacturer = await AddManufacturerAsync("Integration Edit Manufacturer");

        Assert.Equal(
            HttpStatusCode.OK,
            await GetStatusAsync($"/Manufacturers/Edit/{manufacturer.Id}"));
    }

    [Theory]
    [InlineData("Details")]
    [InlineData("Edit")]
    public async Task Manufacturer_Page_WhenManufacturerDoesNotExist_Returns_NotFound(string action)
    {
        Assert.Equal(
            HttpStatusCode.NotFound,
            await GetStatusAsync($"/Manufacturers/{action}/{NonExistingId}"));
    }

    #endregion

    #region Search / Paging

    [Fact]
    public async Task Manufacturers_Index_WithSearch_ReturnsMatchingManufacturer()
    {
        await AddManufacturerAsync("Integration Search Mazda");
        await AddManufacturerAsync("Integration Other Manufacturer");

        var (response, html) = await GetPageAsync("/Manufacturers/Index?search=Search%20Mazda");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Integration Search Mazda", html);
        Assert.DoesNotContain("Integration Other Manufacturer", html);
    }

    [Fact]
    public async Task Manufacturers_Index_Search_IsTrimmed()
    {
        await AddManufacturerAsync("Trimmed Search Manufacturer");

        var (response, html) = await GetPageAsync("/Manufacturers/Index?search=%20Trimmed%20Search%20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Trimmed Search Manufacturer", html);
    }

    [Fact]
    public async Task Manufacturers_Index_Page2_ReturnsSecondPage()
    {
        foreach (var i in Enumerable.Range(1, 11))
        {
            await AddManufacturerAsync($"Pagination Manufacturer {i:00}");
        }
        var (response, html) = await GetPageAsync("/Manufacturers/Index?page=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Page size is 10.
        Assert.Contains("Pagination Manufacturer 11", html);
        Assert.DoesNotContain("Pagination Manufacturer 01", html);
    }

    #endregion

    #region Delete

    [Fact]
    public async Task Manufacturer_Delete_WhenManufacturerDoesNotExist_Redirects()
    {
        var response = await PostDeleteAsync(NonExistingId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Manufacturers", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Manufacturer_Delete_WhenManufacturerExists_SoftDeletesManufacturer()
    {
        var manufacturer = await AddManufacturerAsync("Integration Delete Manufacturer");

        var response = await PostDeleteAsync(manufacturer.Id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var deleted = await GetManufacturerByIdAsync(manufacturer.Id);

        Assert.True(deleted.IsDeleted);
    }

    [Fact]
    public async Task Manufacturer_Delete_DefaultManufacturer_CannotBeDeleted()
    {
        var manufacturer = await Db.Manufacturers
            .FirstAsync(m => m.IsDefault && !m.IsDeleted);

        var response = await PostDeleteAsync(manufacturer.Id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var unchanged = await GetManufacturerByIdAsync(manufacturer.Id);

        Assert.False(unchanged.IsDeleted);
    }

    [Fact]
    public async Task Manufacturer_Delete_WhenManufacturerHasVehicles_CannotBeDeleted()
    {
        var manufacturer = await AddManufacturerAsync("Manufacturer With Vehicle");

        var category = await Db.VehicleCategories
            .FirstAsync(c => !c.IsDeleted);

        Db.Vehicles.Add(new Vehicle
        {
            OwnerName = "Manufacturer Delete Test Vehicle",
            ManufacturerId = manufacturer.Id,
            CategoryId = category.Id,
            YearOfManufacture = 2020,
            Weight = category.MinWeight,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        });

        await Db.SaveChangesAsync();

        // Verify the test data exists before testing deletion.
        var vehicleCount = await Db.Vehicles
            .CountAsync(v => v.ManufacturerId == manufacturer.Id && !v.IsDeleted);

        Assert.Equal(1, vehicleCount);

        var response = await PostDeleteAsync(manufacturer.Id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var unchanged = await GetManufacturerByIdAsync(manufacturer.Id);

        Assert.False(unchanged.IsDeleted);
    }

    #endregion

    #region HTTP Create / Edit

    [Fact]
    public async Task Manufacturer_Create_Post_WithValidData_CreatesManufacturer()
    {
        var response = await PostFormAsync(
            "/Manufacturers/Create",
            "/Manufacturers/Create",
            new Dictionary<string, string>
            {
                ["Name"] = "HTTP Created Manufacturer"
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var manufacturer = await Db.Manufacturers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Name == "HTTP Created Manufacturer");

        Assert.NotNull(manufacturer);
        Assert.False(manufacturer.IsDeleted);
        Assert.False(manufacturer.IsDefault);
    }

    [Fact]
    public async Task Manufacturer_Edit_Post_WithValidData_UpdatesManufacturer()
    {
        var manufacturer = await AddManufacturerAsync("Manufacturer Before Edit");

        var response = await PostFormAsync(
            $"/Manufacturers/Edit/{manufacturer.Id}",
            "/Manufacturers/Edit",
            new Dictionary<string, string>
            {
                ["Id"] = manufacturer.Id.ToString(),
                ["Name"] = "Manufacturer After Edit"
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var updated = await GetManufacturerByIdAsync(manufacturer.Id);

        Assert.Equal("Manufacturer After Edit", updated.Name);
    }

    #endregion
}