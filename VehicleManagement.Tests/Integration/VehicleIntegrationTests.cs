using System.Net;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VehicleManagement.Data;
using VehicleManagement.Models;
using VehicleManagement.Services;
using VehicleManagement.ViewModels;

namespace VehicleManagement.Tests.Integration;

public class VehicleIntegrationTests : IntegrationTestBase
{
    private const int NonExistingId = 999999;

    private IVehicleService VehicleService =>
        Scope.ServiceProvider.GetRequiredService<IVehicleService>();

    private ApplicationDbContext DbContext =>
        Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    #region Helpers

    private HttpClient CreateNoRedirectClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

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

    private Task<Manufacturer> GetManufacturerAsync() =>
        DbContext.Manufacturers.FirstAsync(m => !m.IsDeleted);

    private Task<VehicleCategory> GetCategoryAsync(string name) =>
        DbContext.VehicleCategories.FirstAsync(c => !c.IsDeleted && c.Name == name);

    private static Vehicle NewVehicle(
        string ownerName,
        int manufacturerId,
        int categoryId,
        int year = 2020,
        int weight = 1000) => new()
    {
        OwnerName = ownerName,
        ManufacturerId = manufacturerId,
        YearOfManufacture = year,
        Weight = weight,
        CategoryId = categoryId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsDeleted = false
    };

    private async Task<Vehicle> AddVehicleAsync(
        string ownerName,
        string categoryName = "Medium",
        int year = 2020,
        int weight = 1000)
    {
        var manufacturer = await GetManufacturerAsync();
        var category = await GetCategoryAsync(categoryName);

        var vehicle = NewVehicle(ownerName, manufacturer.Id, category.Id, year, weight);

        DbContext.Vehicles.Add(vehicle);
        await DbContext.SaveChangesAsync();

        return vehicle;
    }

    private Task<Vehicle> GetVehicleAsync(string ownerName) =>
        DbContext.Vehicles.AsNoTracking().FirstAsync(v => v.OwnerName == ownerName);

    private static VehicleFormViewModel NewForm(
        string ownerName,
        int manufacturerId,
        int weight,
        int? id = null) => new()
    {
        Id = id ?? 0,
        OwnerName = ownerName,
        ManufacturerId = manufacturerId,
        YearOfManufacture = 2020,
        Weight = weight
    };

    #endregion

    #region Pages (GET)

    [Fact]
    public async Task CreateVehicle_Get_ReturnsCreatePage()
    {
        // Arrange
        using var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Vehicles/Create");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Create", content);
        Assert.Contains("OwnerName", content);
    }

    [Fact]
    public async Task Vehicles_Index_Returns_Success()
    {
        // Arrange
        await using var factory = new VehicleApiFactory();
        await TestDatabase.InitializeAsync(factory.Services);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Vehicles/Index");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Vehicles_Index_Contains_Vehicles_Page()
    {
        // Arrange
        await using var factory = new VehicleApiFactory();
        await TestDatabase.InitializeAsync(factory.Services);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Vehicles/Index");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Vehicles", content);
    }

    [Fact]
    public async Task EditVehicle_Get_ReturnsEditPage()
    {
        // Arrange
        var vehicle = await AddVehicleAsync("Edit Page Integration Test");
        using var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/Vehicles/Edit/{vehicle.Id}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Edit Page Integration Test", content);
    }

    [Fact]
    public async Task EditVehicle_Get_WhenVehicleDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        using var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/Vehicles/Edit/{NonExistingId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Service: category assignment

    [Theory]
    [InlineData("Light", 100)]
    [InlineData("Medium", 1000)]
    [InlineData("Heavy", 3000)]
    public async Task CreateVehicle_AssignsCategoryByWeight(string categoryName, int weight)
    {
        // Arrange
        var ownerName = $"{categoryName} Integration Test";
        var manufacturer = await GetManufacturerAsync();
        var expectedCategory = await GetCategoryAsync(categoryName);

        // Act
        var result = VehicleService.Create(NewForm(ownerName, manufacturer.Id, weight));

        // Assert
        Assert.True(result.Success);

        var vehicle = await GetVehicleAsync(ownerName);
        Assert.Equal(expectedCategory.Id, vehicle.CategoryId);
    }

    [Fact]
    public async Task CreateVehicle_AtCategoryBoundaries_AssignsCorrectCategory()
    {
        // Arrange
        var manufacturer = await GetManufacturerAsync();
        var mediumCategory = await GetCategoryAsync("Medium");
        var heavyCategory = await GetCategoryAsync("Heavy");

        // Act & Assert - 500 belongs to Medium.
        var mediumResult = VehicleService.Create(
            NewForm("Boundary Medium Test", manufacturer.Id, 500));

        Assert.True(mediumResult.Success);
        Assert.Equal(
            mediumCategory.Id,
            (await GetVehicleAsync("Boundary Medium Test")).CategoryId);

        // Act & Assert - 2500 belongs to Heavy.
        var heavyResult = VehicleService.Create(
            NewForm("Boundary Heavy Test", manufacturer.Id, 2500));

        Assert.True(heavyResult.Success);
        Assert.Equal(
            heavyCategory.Id,
            (await GetVehicleAsync("Boundary Heavy Test")).CategoryId);
    }

    [Fact]
    public async Task UpdateVehicle_WhenWeightChanges_RecalculatesCategory()
    {
        // Arrange
        var manufacturer = await GetManufacturerAsync();
        var mediumCategory = await GetCategoryAsync("Medium");
        var heavyCategory = await GetCategoryAsync("Heavy");

        var createResult = VehicleService.Create(
            NewForm("Category Update Integration Test", manufacturer.Id, 1000));

        Assert.True(createResult.Success);

        var vehicle = await DbContext.Vehicles
            .FirstAsync(v => v.OwnerName == "Category Update Integration Test");

        Assert.Equal(mediumCategory.Id, vehicle.CategoryId);

        // Act - change the weight from Medium to Heavy.
        var updateResult = VehicleService.Update(
            NewForm(vehicle.OwnerName, manufacturer.Id, 3000, vehicle.Id));

        // Assert
        Assert.True(updateResult.Success);

        var updatedVehicle = await DbContext.Vehicles
            .AsNoTracking()
            .FirstAsync(v => v.Id == vehicle.Id);

        Assert.Equal(3000, updatedVehicle.Weight);
        Assert.Equal(heavyCategory.Id, updatedVehicle.CategoryId);
    }

    #endregion

    #region Service: failures

    [Fact]
    public async Task CreateVehicle_WithInvalidManufacturer_Fails()
    {
        // Arrange
        var model = NewForm("Invalid Manufacturer Test", NonExistingId, 1000);

        // Act
        var result = VehicleService.Create(model);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("The selected manufacturer does not exist.", result.ErrorMessage);
        Assert.False(await DbContext.Vehicles.AnyAsync(v => v.OwnerName == "Invalid Manufacturer Test"));
    }

    [Fact]
    public async Task CreateVehicle_WhenWeightIsOutsideAllCategories_Fails()
    {
        // Arrange - a weight outside the configured category ranges.
        var manufacturer = await GetManufacturerAsync();
        var model = NewForm("Invalid Weight Test", manufacturer.Id, -10);

        // Act
        var result = VehicleService.Create(model);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(
            "No vehicle category covers this weight. Check the category ranges.",
            result.ErrorMessage);
        Assert.False(await DbContext.Vehicles.AnyAsync(v => v.OwnerName == "Invalid Weight Test"));
    }

    [Fact]
    public async Task UpdateVehicle_WhenVehicleDoesNotExist_Fails()
    {
        // Arrange
        var manufacturer = await GetManufacturerAsync();
        var model = NewForm("Non Existing Vehicle", manufacturer.Id, 1000, NonExistingId);

        // Act
        var result = VehicleService.Update(model);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Vehicle could not be found.", result.ErrorMessage);
        Assert.False(await DbContext.Vehicles.AnyAsync(v => v.Id == NonExistingId));
    }

    [Fact]
    public async Task DeleteVehicle_WhenVehicleDoesNotExist_Fails()
    {
        // Act
        var result = VehicleService.Delete(NonExistingId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Vehicle could not be found.", result.ErrorMessage);
        Assert.False(await DbContext.Vehicles.AnyAsync(v => v.Id == NonExistingId));
    }

    [Fact]
    public async Task DeleteVehicle_SetsIsDeletedTrue()
    {
        // Arrange
        var vehicle = await AddVehicleAsync("Soft Delete Integration Test");

        // Act
        var result = VehicleService.Delete(vehicle.Id);

        // Assert
        Assert.True(result.Success);

        var deletedVehicle = await DbContext.Vehicles
            .AsNoTracking()
            .FirstAsync(v => v.Id == vehicle.Id);

        Assert.True(deletedVehicle.IsDeleted);
    }

    #endregion

    #region HTTP: create / edit / delete

    [Fact]
    public async Task CreateVehicle_Post_WithValidData_CreatesVehicle()
    {
        // Arrange
        using var client = CreateNoRedirectClient();
        var token = await GetAntiForgeryTokenAsync(client, "/Vehicles/Create");
        var manufacturer = await GetManufacturerAsync();

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["OwnerName"] = "HTTP Integration Vehicle",
            ["ManufacturerId"] = manufacturer.Id.ToString(),
            ["YearOfManufacture"] = "2020",
            ["Weight"] = "1000"
        });

        // Act
        var response = await client.PostAsync("/Vehicles/Create", form);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var vehicle = await DbContext.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.OwnerName == "HTTP Integration Vehicle");

        Assert.NotNull(vehicle);
        Assert.Equal(1000, vehicle.Weight);
        Assert.False(vehicle.IsDeleted);
    }

    [Fact]
    public async Task EditVehicle_Post_WhenWeightChanges_UpdatesVehicleAndCategory()
    {
        // Arrange
        var vehicle = await AddVehicleAsync("HTTP Edit Integration Test");
        var heavyCategory = await GetCategoryAsync("Heavy");

        using var client = CreateNoRedirectClient();
        var token = await GetAntiForgeryTokenAsync(client, $"/Vehicles/Edit/{vehicle.Id}");

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Id"] = vehicle.Id.ToString(),
            ["OwnerName"] = vehicle.OwnerName,
            ["ManufacturerId"] = vehicle.ManufacturerId.ToString(),
            ["YearOfManufacture"] = "2020",
            ["Weight"] = "3000"
        });

        // Act
        var response = await client.PostAsync("/Vehicles/Edit", form);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var updatedVehicle = await DbContext.Vehicles
            .AsNoTracking()
            .FirstAsync(v => v.Id == vehicle.Id);

        Assert.Equal(3000, updatedVehicle.Weight);
        Assert.Equal(heavyCategory.Id, updatedVehicle.CategoryId);
    }

    [Fact]
    public async Task DeleteVehicle_Post_SoftDeletesVehicle()
    {
        // Arrange
        var vehicle = await AddVehicleAsync("HTTP Delete Integration Test");

        using var client = CreateNoRedirectClient();
        var token = await GetAntiForgeryTokenAsync(client, "/Vehicles/Index");

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["id"] = vehicle.Id.ToString()
        });

        // Act
        var response = await client.PostAsync("/Vehicles/Delete", form);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var deletedVehicle = await DbContext.Vehicles
            .AsNoTracking()
            .FirstAsync(v => v.Id == vehicle.Id);

        Assert.True(deletedVehicle.IsDeleted);
    }

    [Fact]
    public async Task DeleteVehicle_Post_WhenVehicleDoesNotExist_RedirectsWithError()
    {
        // Arrange
        using var client = CreateNoRedirectClient();
        var token = await GetAntiForgeryTokenAsync(client, "/Vehicles/Index");

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["id"] = NonExistingId.ToString()
        });

        // Act
        var response = await client.PostAsync("/Vehicles/Delete", form);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    #endregion

    #region HTTP: search / paging

    [Fact]
    public async Task Vehicles_Index_WithSearch_ReturnsMatchingVehicle()
    {
        // Arrange
        await AddVehicleAsync("Search Matching Vehicle");
        await AddVehicleAsync("Different Vehicle", year: 2021);

        using var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Vehicles/Index?search=Search%20Matching");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Search Matching Vehicle", html);
        Assert.DoesNotContain("Different Vehicle", html);
    }

    [Fact]
    public async Task Vehicles_Index_Page2_ReturnsSecondPageOfVehicles()
    {
        // Arrange
        var manufacturer = await GetManufacturerAsync();
        var category = await GetCategoryAsync("Medium");

        DbContext.Vehicles.AddRange(
            Enumerable.Range(1, 11).Select(i =>
                NewVehicle($"Pagination Vehicle {i:00}", manufacturer.Id, category.Id)));

        await DbContext.SaveChangesAsync();

        using var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Vehicles/Index?page=2");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Pagination Vehicle 11", html);
        Assert.DoesNotContain("Pagination Vehicle 01", html);
    }

    #endregion
}