using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VehicleManagement.Data;
using VehicleManagement.Models;
using VehicleManagement.Services;
using VehicleManagement.ViewModels;
using System.Net.Http.Headers;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VehicleManagement.Tests.Integration;

public class VehicleIntegrationTests : IntegrationTestBase
{

    private async Task<(HttpClient Client, string Token)> CreateClientWithAntiForgeryToken()
{
     var client = Factory.CreateClient();

    var response = await client.GetAsync("/Vehicles/Create");

    response.EnsureSuccessStatusCode();

    var html = await response.Content.ReadAsStringAsync();

    var parser = new HtmlParser();
    var document = await parser.ParseDocumentAsync(html);

    var token = document
        .QuerySelector("input[name='__RequestVerificationToken']")
        ?.GetAttribute("value");

    Assert.False(
        string.IsNullOrWhiteSpace(token),
        "Anti-forgery token was not found on the Create page.");

    return (client, token!);
}

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
    public async Task CreateVehicle_WithMediumWeight_AssignsMediumCategory()
    {
        // Arrange

        var service = Scope.ServiceProvider
            .GetRequiredService<IVehicleService>();

        var dbContext = Scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var manufacturer = await dbContext.Manufacturers
            .FirstAsync(m => !m.IsDeleted);

        var mediumCategory = await dbContext.VehicleCategories
            .FirstAsync(c =>
                !c.IsDeleted &&
                c.Name == "Medium");

        var model = new VehicleFormViewModel
        {
            OwnerName = "Integration Test Vehicle",
            ManufacturerId = manufacturer.Id,
            YearOfManufacture = 2020,
            Weight = 1000
        };

        // Act
        var result = service.Create(model);

        // Assert
        Assert.True(result.Success);

        var vehicle = await dbContext.Vehicles
            .AsNoTracking()
            .FirstAsync(v => v.OwnerName == "Integration Test Vehicle");

        Assert.Equal(mediumCategory.Id, vehicle.CategoryId);
    }

    [Fact]
    public async Task CreateVehicle_WithLightWeight_AssignsLightCategory()
    {
        // Arrange

        var service = Scope.ServiceProvider
            .GetRequiredService<IVehicleService>();

        var dbContext = Scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var manufacturer = await dbContext.Manufacturers
            .FirstAsync(m => !m.IsDeleted);

        var lightCategory = await dbContext.VehicleCategories
            .FirstAsync(c => !c.IsDeleted && c.Name == "Light");

        var model = new VehicleFormViewModel
        {
            OwnerName = "Light Integration Test",
            ManufacturerId = manufacturer.Id,
            YearOfManufacture = 2020,
            Weight = 100
        };

        // Act
        var result = service.Create(model);

        // Assert
        Assert.True(result.Success);

        var vehicle = await dbContext.Vehicles
            .AsNoTracking()
            .FirstAsync(v => v.OwnerName == "Light Integration Test");

        Assert.Equal(lightCategory.Id, vehicle.CategoryId);
    }
    [Fact]
    public async Task CreateVehicle_WithHeavyWeight_AssignsHeavyCategory()
    {
        // Arrange

        var service = Scope.ServiceProvider
            .GetRequiredService<IVehicleService>();

        var dbContext = Scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var manufacturer = await dbContext.Manufacturers
            .FirstAsync(m => !m.IsDeleted);

        var heavyCategory = await dbContext.VehicleCategories
            .FirstAsync(c => !c.IsDeleted && c.Name == "Heavy");

        var model = new VehicleFormViewModel
        {
            OwnerName = "Heavy Integration Test",
            ManufacturerId = manufacturer.Id,
            YearOfManufacture = 2020,
            Weight = 3000
        };

        // Act
        var result = service.Create(model);

        // Assert
        Assert.True(result.Success);

        var vehicle = await dbContext.Vehicles
            .AsNoTracking()
            .FirstAsync(v => v.OwnerName == "Heavy Integration Test");

        Assert.Equal(heavyCategory.Id, vehicle.CategoryId);
    }

    [Fact]
    public async Task UpdateVehicle_WhenWeightChanges_RecalculatesCategory()
    {
        // Arrange       

        var service = Scope.ServiceProvider
            .GetRequiredService<IVehicleService>();

        var dbContext = Scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var manufacturer = await dbContext.Manufacturers
            .FirstAsync(m => !m.IsDeleted);

        var mediumCategory = await dbContext.VehicleCategories
            .FirstAsync(c => !c.IsDeleted && c.Name == "Medium");

        var heavyCategory = await dbContext.VehicleCategories
            .FirstAsync(c => !c.IsDeleted && c.Name == "Heavy");

        // Create a vehicle in the Medium range.
        var createModel = new VehicleFormViewModel
        {
            OwnerName = "Category Update Integration Test",
            ManufacturerId = manufacturer.Id,
            YearOfManufacture = 2020,
            Weight = 1000
        };

        var createResult = service.Create(createModel);

        Assert.True(createResult.Success);

        var vehicle = await dbContext.Vehicles
            .FirstAsync(v => v.OwnerName == "Category Update Integration Test");

        Assert.Equal(mediumCategory.Id, vehicle.CategoryId);

        // Act - change the weight from Medium to Heavy.
        var updateModel = new VehicleFormViewModel
        {
            Id = vehicle.Id,
            OwnerName = vehicle.OwnerName,
            ManufacturerId = manufacturer.Id,
            YearOfManufacture = vehicle.YearOfManufacture,
            Weight = 3000
        };

        var updateResult = service.Update(updateModel);

        // Assert
        Assert.True(updateResult.Success);

        var updatedVehicle = await dbContext.Vehicles
            .AsNoTracking()
            .FirstAsync(v => v.Id == vehicle.Id);

        Assert.Equal(3000, updatedVehicle.Weight);
        Assert.Equal(heavyCategory.Id, updatedVehicle.CategoryId);
    }
    [Fact]
    public async Task CreateVehicle_WithInvalidManufacturer_Fails()
    {
        // Arrange
        
        var service = Scope.ServiceProvider
            .GetRequiredService<IVehicleService>();

        var dbContext = Scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var model = new VehicleFormViewModel
        {
            OwnerName = "Invalid Manufacturer Test",
            ManufacturerId = 999999,
            YearOfManufacture = 2020,
            Weight = 1000
        };

        // Act
        var result = service.Create(model);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(
            "The selected manufacturer does not exist.",
            result.ErrorMessage);

        var vehicleExists = await dbContext.Vehicles
            .AnyAsync(v => v.OwnerName == "Invalid Manufacturer Test");

        Assert.False(vehicleExists);
    }

    [Fact]
    public async Task CreateVehicle_WhenWeightIsOutsideAllCategories_Fails()
    {
        // Arrange        
        var service = Scope.ServiceProvider
            .GetRequiredService<IVehicleService>();

        var dbContext = Scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var manufacturer = await dbContext.Manufacturers
            .FirstAsync(m => !m.IsDeleted);

        // Use a weight that is outside the configured category ranges.
        var model = new VehicleFormViewModel
        {
            OwnerName = "Invalid Weight Test",
            ManufacturerId = manufacturer.Id,
            YearOfManufacture = 2020,
            Weight = -10
        };

        // Act
        var result = service.Create(model);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(
            "No vehicle category covers this weight. Check the category ranges.",
            result.ErrorMessage);

        var vehicleExists = await dbContext.Vehicles
            .AnyAsync(v => v.OwnerName == "Invalid Weight Test");

        Assert.False(vehicleExists);
    }

    [Fact]
public async Task UpdateVehicle_WhenVehicleDoesNotExist_Fails()
{
    // Arrange
    
    var service = Scope.ServiceProvider
        .GetRequiredService<IVehicleService>();

    var dbContext = Scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    var manufacturer = await dbContext.Manufacturers
        .FirstAsync(m => !m.IsDeleted);

    var model = new VehicleFormViewModel
    {
        Id = 999999,
        OwnerName = "Non Existing Vehicle",
        ManufacturerId = manufacturer.Id,
        YearOfManufacture = 2020,
        Weight = 1000
    };

    // Act
    var result = service.Update(model);

    // Assert
    Assert.False(result.Success);
    Assert.Equal(
        "Vehicle could not be found.",
        result.ErrorMessage);

    var vehicleExists = await dbContext.Vehicles
        .AnyAsync(v => v.Id == 999999);

    Assert.False(vehicleExists);
}
[Fact]
public async Task DeleteVehicle_WhenVehicleDoesNotExist_Fails()
{
    // Arrange
    
    var service = Scope.ServiceProvider
        .GetRequiredService<IVehicleService>();

    var dbContext = Scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    const int nonExistingVehicleId = 999999;

    // Act
    var result = service.Delete(nonExistingVehicleId);

    // Assert
    Assert.False(result.Success);
    Assert.Equal(
        "Vehicle could not be found.",
        result.ErrorMessage);

    var vehicleExists = await dbContext.Vehicles
        .AnyAsync(v => v.Id == nonExistingVehicleId);

    Assert.False(vehicleExists);
}
[Fact]
public async Task DeleteVehicle_SetsIsDeletedTrue()
{
    // Arrange
        var service = Scope.ServiceProvider
        .GetRequiredService<IVehicleService>();

    var dbContext = Scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    var manufacturer = await dbContext.Manufacturers
        .FirstAsync(m => !m.IsDeleted);

    var vehicle = new Vehicle
    {
        OwnerName = "Soft Delete Integration Test",
        ManufacturerId = manufacturer.Id,
        YearOfManufacture = 2020,
        Weight = 1000,
        CategoryId = await dbContext.VehicleCategories
            .Where(c => !c.IsDeleted && c.Name == "Medium")
            .Select(c => c.Id)
            .FirstAsync(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsDeleted = false
    };

    dbContext.Vehicles.Add(vehicle);
    await dbContext.SaveChangesAsync();

    var vehicleId = vehicle.Id;

    // Act
    var result = service.Delete(vehicleId);

    // Assert
    Assert.True(result.Success);

    var deletedVehicle = await dbContext.Vehicles
        .AsNoTracking()
        .FirstAsync(v => v.Id == vehicleId);

    Assert.True(deletedVehicle.IsDeleted);
}
[Fact]
public async Task CreateVehicle_AtCategoryBoundaries_AssignsCorrectCategory()
{
    // Arrange
    var service = Scope.ServiceProvider
        .GetRequiredService<IVehicleService>();

    var dbContext = Scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    var manufacturer = await dbContext.Manufacturers
        .FirstAsync(m => !m.IsDeleted);

    var lightCategory = await dbContext.VehicleCategories
        .FirstAsync(c => !c.IsDeleted && c.Name == "Light");

    var mediumCategory = await dbContext.VehicleCategories
        .FirstAsync(c => !c.IsDeleted && c.Name == "Medium");

    var heavyCategory = await dbContext.VehicleCategories
        .FirstAsync(c => !c.IsDeleted && c.Name == "Heavy");

    // Act & Assert - 500 belongs to Medium.
    var mediumModel = new VehicleFormViewModel
    {
        OwnerName = "Boundary Medium Test",
        ManufacturerId = manufacturer.Id,
        YearOfManufacture = 2020,
        Weight = 500
    };

    var mediumResult = service.Create(mediumModel);

    Assert.True(mediumResult.Success);

    var mediumVehicle = await dbContext.Vehicles
        .AsNoTracking()
        .FirstAsync(v => v.OwnerName == "Boundary Medium Test");

    Assert.Equal(mediumCategory.Id, mediumVehicle.CategoryId);

    // Act & Assert - 2500 belongs to Heavy.
    var heavyModel = new VehicleFormViewModel
    {
        OwnerName = "Boundary Heavy Test",
        ManufacturerId = manufacturer.Id,
        YearOfManufacture = 2020,
        Weight = 2500
    };

    var heavyResult = service.Create(heavyModel);

    Assert.True(heavyResult.Success);

    var heavyVehicle = await dbContext.Vehicles
        .AsNoTracking()
        .FirstAsync(v => v.OwnerName == "Boundary Heavy Test");

    Assert.Equal(heavyCategory.Id, heavyVehicle.CategoryId);
}
[Fact]
public async Task CreateVehicle_Post_WithValidData_CreatesVehicle()
{
    // Arrange
   using var client = Factory.CreateClient(
    new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    // Get the Create page first.
    var getResponse = await client.GetAsync("/Vehicles/Create");

    Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

    var html = await getResponse.Content.ReadAsStringAsync();

    // Find the anti-forgery token.
    var parser = new AngleSharp.Html.Parser.HtmlParser();
    var document = await parser.ParseDocumentAsync(html);

    var token = document
        .QuerySelector("input[name='__RequestVerificationToken']")
        ?.GetAttribute("value");

    Assert.False(
        string.IsNullOrWhiteSpace(token),
        "Create page did not contain an anti-forgery token.");

    var dbContext = Scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    var manufacturer = await dbContext.Manufacturers
        .FirstAsync(m => !m.IsDeleted);

    var formData = new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = token!,
        ["OwnerName"] = "HTTP Integration Vehicle",
        ["ManufacturerId"] = manufacturer.Id.ToString(),
        ["YearOfManufacture"] = "2020",
        ["Weight"] = "1000"
    };

    using var form = new FormUrlEncodedContent(formData);

    // Act
    var response = await client.PostAsync(
        "/Vehicles/Create",
        form);

    // Diagnostic information
    var responseBody = await response.Content.ReadAsStringAsync();

    Console.WriteLine($"POST Status: {response.StatusCode}");
    Console.WriteLine($"POST Response Length: {responseBody.Length}");

    // Assert
    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

    var vehicle = await dbContext.Vehicles
        .AsNoTracking()
        .FirstOrDefaultAsync(
            v => v.OwnerName == "HTTP Integration Vehicle");

    Assert.NotNull(vehicle);
    Assert.Equal(1000, vehicle.Weight);
    Assert.False(vehicle.IsDeleted);
}
[Fact]
public async Task EditVehicle_Get_ReturnsEditPage()
{
    // Arrange
    var dbContext = Scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    var manufacturer = await dbContext.Manufacturers
        .FirstAsync(m => !m.IsDeleted);

    var category = await dbContext.VehicleCategories
        .FirstAsync(c => !c.IsDeleted && c.Name == "Medium");

    var vehicle = new Vehicle
    {
        OwnerName = "Edit Page Integration Test",
        ManufacturerId = manufacturer.Id,
        YearOfManufacture = 2020,
        Weight = 1000,
        CategoryId = category.Id,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsDeleted = false
    };

    dbContext.Vehicles.Add(vehicle);
    await dbContext.SaveChangesAsync();

    using var client = Factory.CreateClient();

    // Act
    var response = await client.GetAsync(
        $"/Vehicles/Edit/{vehicle.Id}");

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

    const int nonExistingVehicleId = 999999;

    // Act
    var response = await client.GetAsync(
        $"/Vehicles/Edit/{nonExistingVehicleId}");

    // Assert
    Assert.Equal(
        HttpStatusCode.NotFound,
        response.StatusCode);
}
[Fact]
public async Task EditVehicle_Post_WhenWeightChanges_UpdatesVehicleAndCategory()
{
    // Arrange
    var dbContext = Scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    var manufacturer = await dbContext.Manufacturers
        .FirstAsync(m => !m.IsDeleted);

    var mediumCategory = await dbContext.VehicleCategories
        .FirstAsync(c => !c.IsDeleted && c.Name == "Medium");

    var heavyCategory = await dbContext.VehicleCategories
        .FirstAsync(c => !c.IsDeleted && c.Name == "Heavy");

    var vehicle = new Vehicle
    {
        OwnerName = "HTTP Edit Integration Test",
        ManufacturerId = manufacturer.Id,
        YearOfManufacture = 2020,
        Weight = 1000,
        CategoryId = mediumCategory.Id,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsDeleted = false
    };

    dbContext.Vehicles.Add(vehicle);
    await dbContext.SaveChangesAsync();

    using var client = Factory.CreateClient(
    new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    // First GET the Edit page to obtain the anti-forgery token.
    var getResponse = await client.GetAsync(
        $"/Vehicles/Edit/{vehicle.Id}");

    Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

    var html = await getResponse.Content.ReadAsStringAsync();

    var parser = new AngleSharp.Html.Parser.HtmlParser();
    var document = await parser.ParseDocumentAsync(html);

    var token = document
        .QuerySelector("input[name='__RequestVerificationToken']")
        ?.GetAttribute("value");

    Assert.False(
        string.IsNullOrWhiteSpace(token),
        "Edit page did not contain an anti-forgery token.");

    // Prepare edited vehicle.
    var formData = new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = token!,
        ["Id"] = vehicle.Id.ToString(),
        ["OwnerName"] = vehicle.OwnerName,
        ["ManufacturerId"] = manufacturer.Id.ToString(),
        ["YearOfManufacture"] = "2020",
        ["Weight"] = "3000"
    };

    using var form = new FormUrlEncodedContent(formData);

    // Act
    var response = await client.PostAsync(
        "/Vehicles/Edit",
        form);

    // Assert HTTP response
    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

    // Verify database
    var updatedVehicle = await dbContext.Vehicles
        .AsNoTracking()
        .FirstAsync(v => v.Id == vehicle.Id);

    Assert.Equal(3000, updatedVehicle.Weight);
    Assert.Equal(heavyCategory.Id, updatedVehicle.CategoryId);
}
[Fact]
public async Task DeleteVehicle_Post_SoftDeletesVehicle()
{
    // Arrange
    var dbContext = Scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    var manufacturer = await dbContext.Manufacturers
        .FirstAsync(m => !m.IsDeleted);

    var mediumCategory = await dbContext.VehicleCategories
        .FirstAsync(c => !c.IsDeleted && c.Name == "Medium");

    var vehicle = new Vehicle
    {
        OwnerName = "HTTP Delete Integration Test",
        ManufacturerId = manufacturer.Id,
        YearOfManufacture = 2020,
        Weight = 1000,
        CategoryId = mediumCategory.Id,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsDeleted = false
    };

    dbContext.Vehicles.Add(vehicle);
    await dbContext.SaveChangesAsync();

    var vehicleId = vehicle.Id;

    using var client = Factory.CreateClient(
    new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    // Get a page that contains the anti-forgery token.
    var getResponse = await client.GetAsync("/Vehicles/Index");

    Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

    var html = await getResponse.Content.ReadAsStringAsync();

    var parser = new AngleSharp.Html.Parser.HtmlParser();
    var document = await parser.ParseDocumentAsync(html);

    var token = document
        .QuerySelector("input[name='__RequestVerificationToken']")
        ?.GetAttribute("value");

    Assert.False(
        string.IsNullOrWhiteSpace(token),
        "Index page did not contain an anti-forgery token.");

    var formData = new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = token!,
        ["id"] = vehicleId.ToString()
    };

    using var form = new FormUrlEncodedContent(formData);

    // Act
    var response = await client.PostAsync(
        "/Vehicles/Delete",
        form);

    // Assert
    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

    var deletedVehicle = await dbContext.Vehicles
        .AsNoTracking()
        .FirstAsync(v => v.Id == vehicleId);

    Assert.True(deletedVehicle.IsDeleted);
}
[Fact]
public async Task DeleteVehicle_Post_WhenVehicleDoesNotExist_RedirectsWithError()
{
    // Arrange
    using var client = Factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    var getResponse = await client.GetAsync("/Vehicles/Index");

    Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

    var html = await getResponse.Content.ReadAsStringAsync();

    var parser = new AngleSharp.Html.Parser.HtmlParser();
    var document = await parser.ParseDocumentAsync(html);

    var token = document
        .QuerySelector("input[name='__RequestVerificationToken']")
        ?.GetAttribute("value");

    Assert.False(
        string.IsNullOrWhiteSpace(token),
        "Index page did not contain an anti-forgery token.");

    var formData = new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = token!,
        ["id"] = "999999"
    };

    using var form = new FormUrlEncodedContent(formData);

    // Act
    var response = await client.PostAsync(
        "/Vehicles/Delete",
        form);

    // Assert
    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
}
[Fact]
public async Task Vehicles_Index_WithSearch_ReturnsMatchingVehicle()
{
    // Arrange
    var dbContext = Scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    var manufacturer = await dbContext.Manufacturers
        .FirstAsync(m => !m.IsDeleted);

    var category = await dbContext.VehicleCategories
        .FirstAsync(c => !c.IsDeleted && c.Name == "Medium");

    var matchingVehicle = new Vehicle
    {
        OwnerName = "Search Matching Vehicle",
        ManufacturerId = manufacturer.Id,
        YearOfManufacture = 2020,
        Weight = 1000,
        CategoryId = category.Id,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsDeleted = false
    };

    var otherVehicle = new Vehicle
    {
        OwnerName = "Different Vehicle",
        ManufacturerId = manufacturer.Id,
        YearOfManufacture = 2021,
        Weight = 1000,
        CategoryId = category.Id,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsDeleted = false
    };

    dbContext.Vehicles.AddRange(
        matchingVehicle,
        otherVehicle);

    await dbContext.SaveChangesAsync();

    using var client = Factory.CreateClient();

    // Act
    var response = await client.GetAsync(
        "/Vehicles/Index?search=Search%20Matching");

    var html = await response.Content.ReadAsStringAsync();

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    Assert.Contains(
        "Search Matching Vehicle",
        html);

    Assert.DoesNotContain(
        "Different Vehicle",
        html);
}

[Fact]
public async Task Vehicles_Index_Page2_ReturnsSecondPageOfVehicles()
{
    // Arrange
    var dbContext = Scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    var manufacturer = await dbContext.Manufacturers
        .FirstAsync(m => !m.IsDeleted);

    var category = await dbContext.VehicleCategories
        .FirstAsync(c => !c.IsDeleted && c.Name == "Medium");

    for (var i = 1; i <= 11; i++)
    {
        dbContext.Vehicles.Add(new Vehicle
        {
            OwnerName = $"Pagination Vehicle {i:00}",
            ManufacturerId = manufacturer.Id,
            YearOfManufacture = 2020,
            Weight = 1000,
            CategoryId = category.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        });
    }

    await dbContext.SaveChangesAsync();

    using var client = Factory.CreateClient();

    // Act
    var response = await client.GetAsync(
        "/Vehicles/Index?page=2");

    var html = await response.Content.ReadAsStringAsync();

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    Assert.Contains("Pagination Vehicle 11", html);

    Assert.DoesNotContain("Pagination Vehicle 01", html);
}

}