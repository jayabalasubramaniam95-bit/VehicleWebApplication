using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VehicleManagement.Data;
using VehicleManagement.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VehicleManagement.Tests.Integration;

public class ManufacturerIntegrationTests : IntegrationTestBase
{
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
        using var client = Factory.CreateClient();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var manufacturer = await db.Manufacturers
            .AsNoTracking()
            .FirstAsync(m => !m.IsDeleted);

        var response = await client.GetAsync(
            $"/Manufacturers/Details/{manufacturer.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Manufacturer_Details_WhenManufacturerDoesNotExist_Returns_NotFound()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/Manufacturers/Details/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
public async Task Manufacturer_Edit_WhenManufacturerExists_Returns_Success()
{
    using var client = Factory.CreateClient();

    using var scope = Factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    var manufacturer = new Manufacturer
    {
        Name = "Test Edit Manufacturer",
        IsDefault = false,
        IsDeleted = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    db.Manufacturers.Add(manufacturer);
    await db.SaveChangesAsync();

    var response = await client.GetAsync(
        $"/Manufacturers/Edit/{manufacturer.Id}");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}

    [Fact]
    public async Task Manufacturer_Edit_WhenManufacturerDoesNotExist_Returns_NotFound()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/Manufacturers/Edit/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Manufacturer_Delete_WhenManufacturerDoesNotExist_RedirectsWithError()
    {
        using var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var token = await AntiForgeryHelper.GetTokenAsync(client);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/Manufacturers/Delete/999999");

        request.Headers.Add("RequestVerificationToken", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Manufacturers", response.Headers.Location?.OriginalString);
    }
}