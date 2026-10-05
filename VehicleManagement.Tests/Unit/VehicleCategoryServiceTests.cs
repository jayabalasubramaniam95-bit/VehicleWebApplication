using Moq;
using VehicleManagement.Models;
using VehicleManagement.Repositories;
using VehicleManagement.Services;
using VehicleManagement.ViewModels;

namespace VehicleManagement.Tests.Unit;

public class VehicleCategoryServiceTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepository;
    private readonly Mock<IVehicleCategoryRepository> _categoryRepository;
    private readonly VehicleCategoryService _service;

    public VehicleCategoryServiceTests()
    {
        _vehicleRepository = new Mock<IVehicleRepository>();
        _categoryRepository = new Mock<IVehicleCategoryRepository>();

        _service = new VehicleCategoryService(
            _vehicleRepository.Object,
            _categoryRepository.Object);
    }

    #region Helpers

    private static VehicleCategory CreateCategory(int id, string name, int minWeight, int? maxWeight) =>
        new()
        {
            Id = id,
            Name = name,
            MinWeight = minWeight,
            MaxWeight = maxWeight
        };

    private static VehicleCategory Light() => CreateCategory(1, "Light", 0, 500);

    private static VehicleCategory Medium() => CreateCategory(2, "Medium", 500, 2500);

    private static VehicleCategory Heavy() => CreateCategory(3, "Heavy", 2500, null);

    private void SetupRepositories(
        List<VehicleCategory> categories,
        List<Vehicle>? vehicles = null)
    {
        _categoryRepository
            .Setup(r => r.GetAllVehicleCategory())
            .Returns(categories);

        _vehicleRepository
            .Setup(r => r.GetAllVehicles())
            .Returns(vehicles ?? new List<Vehicle>());
    }

    private void AssertCategoryNotUpdated() =>
        _categoryRepository.Verify(r => r.Update(It.IsAny<VehicleCategory>()), Times.Never);

    #endregion

    #region Get Category

    [Fact]
    public void GetVehicleCategoryByWeight_DelegatesToRepository()
    {
        // Arrange
        var category = Medium();

        _categoryRepository
            .Setup(r => r.GetVehicleCategoryByWeight(1200))
            .Returns(category);

        // Act
        var result = _service.GetVehicleCategoryByWeight(1200);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Id);
        Assert.Equal("Medium", result.Name);
    }

    #endregion

    #region Category Range Validation

    [Fact]
    public void Create_WhenNewCategoryContinuesExistingRange_Succeeds()
    {
        // Existing:
        // Light  0 - 500
        // Medium 500 - 2500
        //
        // New category:
        // Heavy  2500 - infinity
        //
        //
        // The new category starts exactly where
        // the existing highest category ends.

        var categories = new List < VehicleCategory >
        {
            new()
            {
                Id = 1, Name = "Light", MinWeight = 0, MaxWeight = 500
            },
            new()
            {
                Id = 2, Name = "Medium", MinWeight = 500, MaxWeight = 2500
            }
        };
        _categoryRepository.Setup(r => r.GetAllVehicleCategory()).Returns(categories);

        // VehicleCategoryService performs Add() inside InTransaction(). 
        // Execute the Action so the code inside the transaction actually runs.
        _categoryRepository.Setup(r => r.InTransaction(It.IsAny<Action>())).Callback<Action>(action => action());
        
        // No existing vehicles are required for this test.
        _vehicleRepository.Setup(r => r.GetAllVehicles()).Returns(new List<Vehicle>());
        var model = new VehicleCategoryFormViewModel
        {
            Name = "Heavy", MinWeight = 2500, MaxWeight = null, Icon = "🚚"
        };

        // Act 
        var result = _service.Create(model);
        // Assert 
        Assert.Equal(CategorySaveStatus.Success, result.Status);
        _categoryRepository.Verify(r => r.Add(It.Is < VehicleCategory > (c => c.Name == "Heavy" && c.MinWeight == 2500 && c.MaxWeight == null)), Times.Once);
    }

    [Fact]
    public void Create_WhenRangeDoesNotConnectToExistingRanges_DoesNotAddCategory()
    {
        // Existing ranges:
        // Light 0 - 500
        // Medium 500 - 2500
        //
        // New range:
        // Extra Heavy 3000 - 5000
        //
        // There is a gap from 2500 to 3000.

        // Arrange
        SetupRepositories(new List<VehicleCategory> { Light(), Medium() });

        var model = new VehicleCategoryFormViewModel
        {
            Name = "Extra Heavy",
            MinWeight = 3000,
            MaxWeight = 5000,
            Icon = "🚛"
        };

        // Act
        var result = _service.Create(model);

        // Assert
        Assert.NotNull(result);

        _categoryRepository.Verify(r => r.Add(It.IsAny<VehicleCategory>()), Times.Never);
    }

    [Fact]
    public void Update_WhenCategoryCreatesGap_DoesNotUpdateRepository()
    {
        // Existing:
        // Light 0 - 500
        // Medium 500 - 2500
        // Heavy 2500 - infinity
        //
        // Try to change Heavy to:
        // Heavy 3000 - infinity
        //
        // This creates a gap from 2500 to 3000.

        // Arrange
        SetupRepositories(new List<VehicleCategory> { Light(), Medium(), Heavy() });

        var model = new VehicleCategoryFormViewModel
        {
            Id = 3,
            Name = "Heavy",
            MinWeight = 3000,
            MaxWeight = null,
            Icon = "🚚"
        };

        // Act
        var result = _service.Update(model);

        // Assert
        Assert.NotNull(result);
        AssertCategoryNotUpdated();
    }

    [Fact]
    public void Update_WhenCategoryCreatesOverlap_DoesNotUpdateRepository()
    {
        // Existing:
        // Light 0 - 500
        // Medium 500 - 2500
        // Heavy 2500 - infinity
        //
        // Try:
        // Medium 500 - 3000
        //
        // Heavy starts at 2500, therefore ranges overlap.

        // Arrange
        SetupRepositories(new List<VehicleCategory> { Light(), Medium(), Heavy() });

        var model = new VehicleCategoryFormViewModel
        {
            Id = 2,
            Name = "Medium",
            MinWeight = 500,
            MaxWeight = 3000,
            Icon = "🚙"
        };

        // Act
        var result = _service.Update(model);

        // Assert
        Assert.NotNull(result);
        AssertCategoryNotUpdated();
    }

    [Fact]
    public void Update_WhenRangeIsInvalid_DoesNotUpdateRepository()
    {
        // Max must be greater than Min.

        // Arrange
        SetupRepositories(new List<VehicleCategory> { Light(), Medium(), Heavy() });

        var model = new VehicleCategoryFormViewModel
        {
            Id = 2,
            Name = "Medium",
            MinWeight = 2500,
            MaxWeight = 2500,
            Icon = "🚙"
        };

        // Act
        var result = _service.Update(model);

        // Assert
        Assert.NotNull(result);
        AssertCategoryNotUpdated();
    }

    [Fact]
    public void Update_WhenLowestCategoryDoesNotStartAtZero_DoesNotUpdate()
    {
        // Arrange
        SetupRepositories(new List<VehicleCategory>
        {
            CreateCategory(1, "Light", 100, 500),
            CreateCategory(2, "Medium", 500, null)
        });

        var model = new VehicleCategoryFormViewModel
        {
            Id = 1,
            Name = "Light",
            MinWeight = 100,
            MaxWeight = 500,
            Icon = "🚗"
        };

        // Act
        var result = _service.Update(model);

        // Assert
        Assert.NotNull(result);
        AssertCategoryNotUpdated();
    }

    #endregion

    #region Vehicle Recategorisation

   [Fact]
    public void Update_WhenExistingVehicleHasWrongCategory_RecategorisesVehicle()
    {
        // Valid ranges:
        //   Light  :    0 - 500
        //   Medium :  500 - 2500
        //   Heavy  : 2500 - infinity
        //
        // The vehicle weighs 1200 kg, so it belongs to Medium (Id = 2).
        // It is deliberately given Heavy (Id = 3) first.

        // Arrange
        var vehicle = new Vehicle
        {
            Id = 100,
            OwnerName = "John",
            Weight = 1200,
            CategoryId = 3 // Deliberately incorrect
        };

        SetupRepositories(
            new List<VehicleCategory> { Light(), Medium(), Heavy() },
            new List<Vehicle> { vehicle });

        // IMPORTANT: the service runs its update inside InTransaction(Action),
        // so the mock must execute the Action supplied by the service.
        _categoryRepository
            .Setup(r => r.InTransaction(It.IsAny<Action>()))
            .Callback<Action>(action => action());

        var model = new VehicleCategoryFormViewModel
        {
            Id = 2,
            Name = "Medium",
            Icon = "🚙",
            MinWeight = 500,
            MaxWeight = 2500
        };

        // Act
        _service.Update(model);

        // Assert

        // 500 <= 1200 < 2500, so the vehicle must be moved to Medium.
        Assert.Equal(2, vehicle.CategoryId);

        // The vehicle's category changed, so it must have been persisted.
        _vehicleRepository.Verify(r => r.Update(vehicle), Times.Once);

        // The category itself must also have been updated.
        _categoryRepository.Verify(
            r => r.Update(It.Is<VehicleCategory>(c =>
                c.Id == 2 &&
                c.Name == "Medium" &&
                c.MinWeight == 500 &&
                c.MaxWeight == 2500)),
            Times.Once);
    }

    [Fact]
    public void Update_WhenVehicleAlreadyHasCorrectCategory_DoesNotUpdateVehicle()
    {
        // Arrange
        var vehicle = new Vehicle
        {
            Id = 100,
            OwnerName = "John",
            Weight = 1200,
            CategoryId = 2
        };

        SetupRepositories(
            new List<VehicleCategory> { Light(), Medium(), Heavy() },
            new List<Vehicle> { vehicle });

        var model = new VehicleCategoryFormViewModel
        {
            Id = 2,
            Name = "Medium",
            MinWeight = 500,
            MaxWeight = 2500,
            Icon = "🚙"
        };

        // Act
        _service.Update(model);

        // Assert
        _vehicleRepository.Verify(r => r.Update(It.IsAny<Vehicle>()), Times.Never);
    }

    #endregion

    #region Delete

    [Fact]
    public void Delete_WhenCategoryDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        _categoryRepository
            .Setup(r => r.GetAllVehicleCategory())
            .Returns(new List<VehicleCategory>());

        // Act
        var result = _service.Delete(999);

        // Assert
        Assert.Equal(CategoryDeleteResult.NotFound, result);
    }

    [Fact]
    public void Delete_WhenCategoryHasVehicles_ReturnsHasVehicles()
    {
        // Arrange
        _categoryRepository
            .Setup(r => r.GetAllVehicleCategory())
            .Returns(new List<VehicleCategory> { Light(), Medium() });

        _categoryRepository
            .Setup(r => r.HasVehicles(2))
            .Returns(true);

        // Act
        var result = _service.Delete(2);

        // Assert
        Assert.Equal(CategoryDeleteResult.HasVehicles, result);
        AssertCategoryNotUpdated();
    }

    [Fact]
    public void Delete_WhenLastCategory_ReturnsLastCategory()
    {
        // Arrange
        _categoryRepository
            .Setup(r => r.GetAllVehicleCategory())
            .Returns(new List<VehicleCategory> { CreateCategory(1, "Light", 0, null) });

        // Act
        var result = _service.Delete(1);

        // Assert
        Assert.Equal(CategoryDeleteResult.LastCategory, result);
    }

    #endregion
}