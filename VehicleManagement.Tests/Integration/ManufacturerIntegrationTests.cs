using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VehicleManagement.Data;
using VehicleManagement.Models;

namespace VehicleManagement.Tests.Integration;

public class ManufacturerIntegrationTests : IntegrationTestBase
{
    private const int NonExistingId = 999999;

    #region Helpers

    private HttpClient CreateNoRedirectClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private static async Task<Manufacturer> AddManufacturerAsync(ApplicationDbContext db, string name)
    {
        var manufacturer = new Manufacturer
        {
            Name = name,
            IsDefault = false,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Manufacturers.Add(manufacturer);
        await db.SaveChangesAsync();

        return manufacturer;
    }

    private static async Task<HttpResponseMessage> PostDeleteAsync(HttpClient client, int id)
    {
        var token = await AntiForgeryHelper.GetTokenAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Manufacturers/Delete/{id}");
        request.Headers.Add("RequestVerificationToken", token);

        return await client.SendAsync(request);
    }

    #endregion

    #region Pages (GET)

    [Fact]
    public async Task Manufacturers_Index_Returns_Success()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/Manufacturers/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Manufacturer_Create_Returns_Success()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/Manufacturers/Create");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Manufacturer_Details_WhenManufacturerExists_Returns_Success()
    {
        // Arrange
        using var client = Factory.CreateClient();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var manufacturer = await db.Manufacturers
            .AsNoTracking()
            .FirstAsync(m => !m.IsDeleted);

        // Act
        var response = await client.GetAsync($"/Manufacturers/Details/{manufacturer.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Manufacturer_Details_WhenManufacturerDoesNotExist_Returns_NotFound()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync($"/Manufacturers/Details/{NonExistingId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Manufacturer_Edit_WhenManufacturerExists_Returns_Success()
    {
        // Arrange
        using var client = Factory.CreateClient();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var manufacturer = await AddManufacturerAsync(db, "Test Edit Manufacturer");

        // Act
        var response = await client.GetAsync($"/Manufacturers/Edit/{manufacturer.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Manufacturer_Edit_WhenManufacturerDoesNotExist_Returns_NotFound()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync($"/Manufacturers/Edit/{NonExistingId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Delete (POST)

    [Fact]
    public async Task Manufacturer_Delete_WhenManufacturerDoesNotExist_RedirectsWithError()
    {
        // Arrange
        using var client = CreateNoRedirectClient();

        // Act
        var response = await PostDeleteAsync(client, NonExistingId);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Manufacturers", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Manufacturer_Delete_WhenManufacturerExists_SoftDeletesManufacturer()
    {
        // Arrange
        using var client = CreateNoRedirectClient();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var manufacturer = await AddManufacturerAsync(db, "Integration Delete Manufacturer");

        // Act
        var response = await PostDeleteAsync(client, manufacturer.Id);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Manufacturers", response.Headers.Location?.OriginalString);

        var deletedManufacturer = await db.Manufacturers
            .AsNoTracking()
            .FirstAsync(m => m.Id == manufacturer.Id);

        Assert.True(deletedManufacturer.IsDeleted);
    }

    #endregion
}