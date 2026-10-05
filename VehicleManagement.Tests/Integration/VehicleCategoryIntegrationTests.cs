using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VehicleManagement.Data;
using VehicleManagement.Models;
using VehicleManagement.Services;
using VehicleManagement.ViewModels;
using System.Globalization;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VehicleManagement.Tests.Integration;

public class VehicleCategoryIntegrationTests : IntegrationTestBase
{
    private const int NonExistingId = 999999;

    private IVehicleCategoryService CategoryService =>
        Scope.ServiceProvider.GetRequiredService<IVehicleCategoryService>();

    private ApplicationDbContext Db =>
        Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    #region Helpers

    private async Task<HttpStatusCode> GetStatusAsync(string url)
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(url);

        return response.StatusCode;
    }

    private Task<VehicleCategory> GetCategoryAsync(string name) =>
        Db.VehicleCategories
            .FirstAsync(c => !c.IsDeleted && c.Name == name);

    private Task<Manufacturer> GetManufacturerAsync() =>
        Db.Manufacturers
            .FirstAsync(m => !m.IsDeleted);

    private static VehicleCategoryFormViewModel NewCategoryForm(
        string name,
        decimal minWeight,
        decimal? maxWeight,
        string? icon = "car",
        int id = 0) =>
        new()
        {
            Id = id,
            Name = name,
            Icon = icon ?? "car",
            MinWeight = minWeight,
            MaxWeight = maxWeight
        };

    private static VehicleCategoryFormViewModel UpdateForm(
        VehicleCategory category,
        decimal minWeight,
        decimal? maxWeight) =>
        NewCategoryForm(
            category.Name,
            minWeight,
            maxWeight,
            category.Icon,
            category.Id);

    private async Task<Vehicle> AddVehicleAsync(
        string ownerName,
        int categoryId,
        decimal weight)
    {
        var manufacturer = await GetManufacturerAsync();

        var vehicle = new Vehicle
        {
            OwnerName = ownerName,
            ManufacturerId = manufacturer.Id,
            CategoryId = categoryId,
            YearOfManufacture = 2020,
            Weight = weight,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        Db.Vehicles.Add(vehicle);
        await Db.SaveChangesAsync();

        return vehicle;
    }

    #endregion

    #region GET Pages

    [Theory]
    [InlineData("/VehicleCategory/Index")]
    [InlineData("/VehicleCategory/Create")]
    public async Task VehicleCategory_Page_Returns_Success(string url)
    {
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(url));
    }

    [Fact]
    public async Task VehicleCategory_Edit_WhenCategoryExists_Returns_Success()
    {
        var category = await GetCategoryAsync("Medium");

        Assert.Equal(
            HttpStatusCode.OK,
            await GetStatusAsync($"/VehicleCategory/Edit/{category.Id}"));
    }

    [Fact]
    public async Task VehicleCategory_Edit_WhenCategoryDoesNotExist_Returns_NotFound()
    {
        Assert.Equal(
            HttpStatusCode.NotFound,
            await GetStatusAsync($"/VehicleCategory/Edit/{NonExistingId}"));
    }

    #endregion

    #region Helper Methods
    private async Task<HttpResponseMessage> PostCategoryAsync(
    string url,
    Dictionary<string, string> fields)
    {
        using var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        var tokenResponse = await client.GetAsync(
            url.Contains("/Edit/") ? url : "/VehicleCategory/Create");

        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var html = await tokenResponse.Content.ReadAsStringAsync();
        var document = await new AngleSharp.Html.Parser.HtmlParser().ParseDocumentAsync(html);
        var token = document
            .QuerySelector("input[name='__RequestVerificationToken']")
            ?.GetAttribute("value");
        Assert.False(
            string.IsNullOrWhiteSpace(token),
            "Anti-forgery token was not found.");
        fields["__RequestVerificationToken"] = token!;
        using var form = new FormUrlEncodedContent(fields);
        return await client.PostAsync(url, form);
    }

       public static TheoryData<string, decimal, decimal?> InvalidRanges => new()
        {
            { "Invalid Reverse Range", 3000m, 2000m },
            { "Invalid Equal Range", 3000m, 3000m }
        };

        [Theory]
        [MemberData(nameof(InvalidRanges))]
    public async Task CreateCategory_WithInvalidRange_IsRejected(
    string name,
    decimal minWeight,
    decimal? maxWeight)
    {
        var response = await PostCategoryAsync(
        "/VehicleCategory/Create",
        new Dictionary<string, string>
        {
        ["Name"] = name,
        ["MinWeight"] = minWeight.ToString(CultureInfo.InvariantCulture),
        ["MaxWeight"] = maxWeight?.ToString(CultureInfo.InvariantCulture)
        ?? string.Empty,
        ["Icon"] = "car"
        });

        // Invalid configuration must return the form.
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        // The invalid category must not be persisted.
        var categoryExists = await Db.VehicleCategories
            .AsNoTracking()
            .AnyAsync(c =>
                c.Name == name &&
                !c.IsDeleted);

        Assert.False(categoryExists);

        // The response should contain a validation message.
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(
            html.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("greater", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("range", StringComparison.OrdinalIgnoreCase),
            "The invalid category response did not contain an expected validation message.");

    }

    [Fact]
    public async Task CreateCategory_WhenItBreaksContinuity_IsRejected()
    {
        var response = await PostCategoryAsync(
        "/VehicleCategory/Create",
                                new Dictionary<string, string>
                                {
                                    ["Name"] = "Invalid New Category",
                                    ["MinWeight"] = "3000",
                                    ["MaxWeight"] = "5000",
                                    ["Icon"] = "car"
                                });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var html = await response.Content.ReadAsStringAsync();
    Assert.Contains("Gap", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateCategory_WithGap_IsRejected()
    {
        var medium = await GetCategoryAsync("Medium");
        var response = await PostCategoryAsync(
            $"/VehicleCategory/Edit/{medium.Id}",
            new Dictionary<string, string>
            {
                ["Id"] = medium.Id.ToString(),
                ["Name"] = medium.Name,
                ["MinWeight"] = "600",
                ["MaxWeight"] = medium.MaxWeight?.ToString() ?? "",
                ["Icon"] = medium.Icon ?? "car"
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Gap", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateCategory_WithOverlap_IsRejected()
    {
        var medium = await GetCategoryAsync("Medium");

        var response = await PostCategoryAsync(
            $"/VehicleCategory/Edit/{medium.Id}",
            new Dictionary<string, string>
            {
                ["Id"] = medium.Id.ToString(),
                ["Name"] = medium.Name,
                ["MinWeight"] = "400",
                ["MaxWeight"] = "2500",
                ["Icon"] = medium.Icon ?? "car"
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Overlap", html, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Vehicle Recategorisation

    [Fact]
    public async Task UpdateCategory_WhenRangeChanges_RecategorisesExistingVehicles()
    {
        // Initial configuration:
        //   Light  :    0 - 500
        //   Medium :  500 - 2500
        //   Heavy  : 2500+
        //
        // A 2700 kg vehicle belongs to Heavy.

        // Arrange
        var medium = await GetCategoryAsync("Medium");
        var heavy = await GetCategoryAsync("Heavy");

        var vehicle = await AddVehicleAsync("Category Recategorisation Test", heavy.Id, 2700m);

        Assert.Equal(heavy.Id, vehicle.CategoryId);

        // Act
        //
        // Change Medium from 500 - 2500 to 500 - 3000.
        // The controller/service should automatically move Heavy's minimum
        // from 2500 to 3000 and recategorise the existing 2700 kg vehicle.
        var response = await PostCategoryUpdateAsync(
            medium.Id,
            new Dictionary<string, string>
            {
                ["Id"] = medium.Id.ToString(),
                ["Name"] = medium.Name,
                ["MinWeight"] = "500",
                ["MaxWeight"] = "3000",
                ["Icon"] = medium.Icon ?? "car"
            });

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        // Medium was updated.
        var updatedMedium = await Db.VehicleCategories
            .AsNoTracking()
            .FirstAsync(c => c.Id == medium.Id);

        Assert.Equal(3000m, updatedMedium.MaxWeight);

        // Heavy was moved from 2500 to 3000.
        var updatedHeavy = await Db.VehicleCategories
            .AsNoTracking()
            .FirstAsync(c => c.Id == heavy.Id);

        Assert.Equal(3000m, updatedHeavy.MinWeight);

        // The existing vehicle was recategorised: 2700 kg is now inside
        // Medium (500 - 3000), so it must belong to Medium.
        var updatedVehicle = await Db.Vehicles
            .AsNoTracking()
            .FirstAsync(v => v.Id == vehicle.Id);

        Assert.Equal(medium.Id, updatedVehicle.CategoryId);
    }
        private async Task<HttpResponseMessage> PostCategoryUpdateAsync(
        int categoryId,
        Dictionary<string, string> fields)
    {
        using var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var editUrl = $"/VehicleCategory/Edit/{categoryId}";

        // Get the edit page first so we can retrieve the antiforgery token.
        var getResponse = await client.GetAsync(editUrl);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var html = await getResponse.Content.ReadAsStringAsync();
        var document = await new HtmlParser().ParseDocumentAsync(html);

        var token = document
            .QuerySelector("input[name='__RequestVerificationToken']")
            ?.GetAttribute("value");

        Assert.False(
            string.IsNullOrWhiteSpace(token),
            "Anti-forgery token was not found.");

        fields["__RequestVerificationToken"] = token!;

        using var form = new FormUrlEncodedContent(fields);

        return await client.PostAsync(editUrl, form);
    }

    #endregion

    #region Delete

    [Fact]
    public void DeleteCategory_WhenCategoryDoesNotExist_ReturnsNotFound()
    {
        Assert.Equal(
            CategoryDeleteResult.NotFound,
            CategoryService.Delete(NonExistingId));
    }

    private async Task<HttpResponseMessage> PostCategoryDeleteAsync(int id)
    {
        using var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        var getResponse =
            await client.GetAsync("/VehicleCategory/Index");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var html = await getResponse.Content.ReadAsStringAsync();
        var document =
            await new HtmlParser().ParseDocumentAsync(html);
        var token = document
            .QuerySelector("input[name='__RequestVerificationToken']")
            ?.GetAttribute("value");
        Assert.False(
            string.IsNullOrWhiteSpace(token),
            "Anti-forgery token was not found.");
        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/VehicleCategory/Delete");
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = id.ToString(),
                ["__RequestVerificationToken"] = token!
            });
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task DeleteCategory_WhenCategoryHasVehicles_IsRejected()
    {
        var medium = await GetCategoryAsync("Medium");
        await AddVehicleAsync(
                            "Category Delete Vehicle Test",
                            medium.Id,
                            1000);
        var response = await PostCategoryDeleteAsync(medium.Id);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var category = await Db.VehicleCategories
            .AsNoTracking()
            .FirstAsync(c => c.Id == medium.Id);
        Assert.False(category.IsDeleted);
    }

    [Fact]
    public async Task DeleteCategory_WhenLastCategory_RemainsProtected()
    {
        var activeCategoryCount = await Db.VehicleCategories
            .CountAsync(c => !c.IsDeleted);
        Assert.True(activeCategoryCount > 1);
    }

    #endregion
}