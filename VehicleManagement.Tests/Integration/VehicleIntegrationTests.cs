using System.Globalization;
using System.Net;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VehicleManagement.Data;
using VehicleManagement.Models;

namespace VehicleManagement.Tests.Integration;

public class VehicleIntegrationTests : IntegrationTestBase
{
    private const int NonExistingId = 999999;

    private ApplicationDbContext Db =>
        Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    #region Helpers

    private HttpClient CreateNoRedirectClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<(HttpResponseMessage Response, string Html)> GetPageAsync(string url)
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();

        return (response, html);
    }

    private static async Task<string> GetAntiForgeryTokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        var document = await new HtmlParser().ParseDocumentAsync(html);

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

        fields["__RequestVerificationToken"] = await GetAntiForgeryTokenAsync(client, tokenUrl);

        using var form = new FormUrlEncodedContent(fields);
        return await client.PostAsync(postUrl, form);
    }

    private Task<Manufacturer> GetManufacturerAsync() =>
        Db.Manufacturers.FirstAsync(m => !m.IsDeleted);

    private Task<VehicleCategory> GetCategoryAsync(string name) =>
        Db.VehicleCategories.FirstAsync(c => !c.IsDeleted && c.Name == name);

    private Task<Vehicle> GetVehicleAsync(string ownerName) =>
        Db.Vehicles.AsNoTracking().FirstAsync(v => !v.IsDeleted && v.OwnerName == ownerName);

    private Task<Vehicle> GetVehicleByIdAsync(int id) =>
        Db.Vehicles.AsNoTracking().FirstAsync(v => v.Id == id);

    private async Task<Vehicle> AddVehicleAsync(
        string ownerName,
        string categoryName = "Medium",
        int year = 2020,
        decimal weight = 1000)
    {
        var manufacturer = await GetManufacturerAsync();
        var category = await GetCategoryAsync(categoryName);

        var vehicle = new Vehicle
        {
            OwnerName = ownerName,
            ManufacturerId = manufacturer.Id,
            CategoryId = category.Id,
            YearOfManufacture = year,
            Weight = weight,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        Db.Vehicles.Add(vehicle);
        await Db.SaveChangesAsync();

        return vehicle;
    }

    private async Task AssertIndexOrderAsync(string query, params string[] expectedOrder)
    {
        var (response, html) = await GetPageAsync($"/Vehicles/Index?{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var positions = expectedOrder
            .Select(text => html.IndexOf(text, StringComparison.Ordinal))
            .ToList();

        Assert.All(positions, position => Assert.True(position >= 0));
        Assert.Equal(positions.OrderBy(x => x), positions);
    }

    #endregion

    #region GET Pages

    [Fact]
    public async Task Vehicles_Index_Returns_Success()
    {
        var (response, _) = await GetPageAsync("/Vehicles/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Vehicles_Index_Contains_Vehicles_Page()
    {
        var (response, html) = await GetPageAsync("/Vehicles/Index");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Vehicles", html);
    }

    [Fact]
    public async Task CreateVehicle_Get_ReturnsCreatePage()
    {
        var (response, html) = await GetPageAsync("/Vehicles/Create");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Create", html);
        Assert.Contains("OwnerName", html);
    }

    [Fact]
    public async Task EditVehicle_Get_ReturnsEditPage()
    {
        var vehicle = await AddVehicleAsync("Edit Page Integration Test");

        var (response, html) = await GetPageAsync($"/Vehicles/Edit/{vehicle.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Edit Page Integration Test", html);
    }

    [Theory]
    [InlineData("Edit")]
    [InlineData("Details")]
    public async Task Vehicle_GetPage_WhenVehicleDoesNotExist_ReturnsNotFound(string action)
    {
        var (response, _) = await GetPageAsync($"/Vehicles/{action}/{NonExistingId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Category Assignment

    [Theory]
    [InlineData(1.0, "Light")]
    [InlineData(100.0, "Light")]
    [InlineData(500.0, "Medium")]
    [InlineData(1000.0, "Medium")]
    [InlineData(2500.0, "Heavy")]
    [InlineData(3000.0, "Heavy")]
    public async Task CreateVehicle_AssignsCorrectCategory(
        decimal weight,
        string expectedCategoryName)
    {
        var manufacturer = await GetManufacturerAsync();
        var category = await GetCategoryAsync(expectedCategoryName);

        var ownerName = $"Category {weight} Integration Test";

        var response = await PostFormAsync(
            "/Vehicles/Create",
            "/Vehicles/Create",
            new Dictionary<string, string>
            {
                ["OwnerName"] = ownerName,
                ["ManufacturerId"] = manufacturer.Id.ToString(),
                ["YearOfManufacture"] = "2020",
                ["Weight"] = weight.ToString(CultureInfo.InvariantCulture)
            });

        // HTTP integration test: successful POST should redirect.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        // Verify the actual database result.
        var vehicle = await GetVehicleAsync(ownerName);

        Assert.Equal(category.Id, vehicle.CategoryId);
    }

    [Fact]
    public async Task UpdateVehicle_WhenWeightChanges_RecalculatesCategory()
    {
        var vehicle = await AddVehicleAsync("Category Update Integration Test", "Medium", weight: 1000);
        var heavy = await GetCategoryAsync("Heavy");

        var response = await PostFormAsync(
            $"/Vehicles/Edit/{vehicle.Id}",
            "/Vehicles/Edit",
            new Dictionary<string, string>
            {
                ["Id"] = vehicle.Id.ToString(),
                ["OwnerName"] = vehicle.OwnerName,
                ["ManufacturerId"] = vehicle.ManufacturerId.ToString(),
                ["YearOfManufacture"] = "2020",
                ["Weight"] = "3000"
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var updated = await GetVehicleByIdAsync(vehicle.Id);

        Assert.Equal(3000, updated.Weight);
        Assert.Equal(heavy.Id, updated.CategoryId);
    }

    #endregion

    #region Validation

    [Fact]
    public async Task CreateVehicle_WithInvalidManufacturer_IsRejected()
    {
        var response = await PostFormAsync(
            "/Vehicles/Create",
            "/Vehicles/Create",
            new Dictionary<string, string>
            {
                ["OwnerName"] = "Invalid Manufacturer Test",
                ["ManufacturerId"] = NonExistingId.ToString(),
                ["YearOfManufacture"] = "2020",
                ["Weight"] = "1000"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await Db.Vehicles.AnyAsync(v => v.OwnerName == "Invalid Manufacturer Test"));
    }

    [Fact]
    public async Task CreateVehicle_WithInvalidWeight_IsRejected()
    {
        var manufacturer = await GetManufacturerAsync();

        var response = await PostFormAsync(
            "/Vehicles/Create",
            "/Vehicles/Create",
            new Dictionary<string, string>
            {
                ["OwnerName"] = "Invalid Weight Test",
                ["ManufacturerId"] = manufacturer.Id.ToString(),
                ["YearOfManufacture"] = "2020",
                ["Weight"] = "-10"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await Db.Vehicles.AnyAsync(v => v.OwnerName == "Invalid Weight Test"));
    }

    #endregion

    #region HTTP Create / Edit / Delete

    [Fact]
    public async Task CreateVehicle_Post_WithValidData_CreatesVehicle()
    {
        var manufacturer = await GetManufacturerAsync();

        var response = await PostFormAsync(
            "/Vehicles/Create",
            "/Vehicles/Create",
            new Dictionary<string, string>
            {
                ["OwnerName"] = "HTTP Integration Vehicle",
                ["ManufacturerId"] = manufacturer.Id.ToString(),
                ["YearOfManufacture"] = "2020",
                ["Weight"] = "1000"
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var vehicle = await Db.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.OwnerName == "HTTP Integration Vehicle");

        Assert.NotNull(vehicle);
        Assert.Equal(1000, vehicle.Weight);
        Assert.False(vehicle.IsDeleted);
    }

    [Fact]
    public async Task EditVehicle_Post_WhenWeightChanges_UpdatesVehicle()
    {
        var vehicle = await AddVehicleAsync("HTTP Edit Integration Test");
        var heavy = await GetCategoryAsync("Heavy");

        var response = await PostFormAsync(
            $"/Vehicles/Edit/{vehicle.Id}",
            "/Vehicles/Edit",
            new Dictionary<string, string>
            {
                ["Id"] = vehicle.Id.ToString(),
                ["OwnerName"] = vehicle.OwnerName,
                ["ManufacturerId"] = vehicle.ManufacturerId.ToString(),
                ["YearOfManufacture"] = "2020",
                ["Weight"] = "3000"
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var updated = await GetVehicleByIdAsync(vehicle.Id);

        Assert.Equal(3000, updated.Weight);
        Assert.Equal(heavy.Id, updated.CategoryId);
    }

    [Fact]
    public async Task DeleteVehicle_Post_SoftDeletesVehicle()
    {
        var vehicle = await AddVehicleAsync("HTTP Delete Integration Test");

        var response = await PostFormAsync(
            "/Vehicles/Index",
            "/Vehicles/Delete",
            new Dictionary<string, string> { ["id"] = vehicle.Id.ToString() });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var deleted = await Db.Vehicles.AsNoTracking().FirstAsync(v => v.Id == vehicle.Id);

        Assert.True(deleted.IsDeleted);
    }

    [Fact]
    public async Task DeleteVehicle_Post_WhenVehicleDoesNotExist_Redirects()
    {
        var response = await PostFormAsync(
            "/Vehicles/Index",
            "/Vehicles/Delete",
            new Dictionary<string, string> { ["id"] = NonExistingId.ToString() });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    #endregion

    #region Search / Paging / Sorting

    [Fact]
    public async Task Vehicles_Index_WithSearch_ReturnsMatchingVehicle()
    {
        await AddVehicleAsync("Search Matching Vehicle");
        await AddVehicleAsync("Different Vehicle", year: 2021);

        var (response, html) = await GetPageAsync("/Vehicles/Index?search=Search%20Matching");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Search Matching Vehicle", html);
        Assert.DoesNotContain("Different Vehicle", html);
    }

    [Fact]
    public async Task Vehicles_Index_Page2_ReturnsSecondPage()
    {
        var manufacturer = await GetManufacturerAsync();
        var category = await GetCategoryAsync("Medium");

        Db.Vehicles.AddRange(
            Enumerable.Range(1, 11).Select(i => new Vehicle
            {
                OwnerName = $"Pagination Vehicle {i:00}",
                ManufacturerId = manufacturer.Id,
                CategoryId = category.Id,
                YearOfManufacture = 2020,
                Weight = 1000,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            }));

        await Db.SaveChangesAsync();

        var (response, html) = await GetPageAsync("/Vehicles/Index?page=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Pagination Vehicle 11", html);
        Assert.DoesNotContain("Pagination Vehicle 01", html);
    }

    [Fact]
    public async Task Vehicles_Index_SortByWeightAscending_ReturnsCorrectOrder()
    {
        await AddVehicleAsync("Weight Sort 300", weight: 300);
        await AddVehicleAsync("Weight Sort 1000", weight: 1000);
        await AddVehicleAsync("Weight Sort 2000", weight: 2000);

        await AssertIndexOrderAsync(
            "sortColumn=weight&sortDirection=asc",
            "Weight Sort 300",
            "Weight Sort 1000",
            "Weight Sort 2000");
    }

    [Fact]
    public async Task Vehicles_Index_SortByWeightDescending_ReturnsCorrectOrder()
    {
        await AddVehicleAsync("Descending Weight 300", weight: 300);
        await AddVehicleAsync("Descending Weight 1000", weight: 1000);
        await AddVehicleAsync("Descending Weight 2000", weight: 2000);

        await AssertIndexOrderAsync(
            "sortColumn=weight&sortDirection=desc",
            "Descending Weight 2000",
            "Descending Weight 1000",
            "Descending Weight 300");
    }

    [Fact]
    public async Task Vehicles_Index_SortByYearAscending_ReturnsCorrectOrder()
    {
        await AddVehicleAsync("Year Sort 2022", year: 2022);
        await AddVehicleAsync("Year Sort 2019", year: 2019);
        await AddVehicleAsync("Year Sort 2025", year: 2025);

        await AssertIndexOrderAsync(
            "sortColumn=year&sortDirection=asc",
            "Year Sort 2019",
            "Year Sort 2022",
            "Year Sort 2025");
    }

    #endregion
}