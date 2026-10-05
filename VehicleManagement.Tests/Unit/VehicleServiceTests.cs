using Moq;
using VehicleManagement.Models;
using VehicleManagement.Repositories;
using VehicleManagement.Services;
using VehicleManagement.ViewModels;

namespace VehicleManagement.Tests.Unit;

public class VehicleServiceTests
{
    private const string VehicleNotFoundMessage = "Vehicle could not be found.";

    private readonly Mock<IVehicleRepository> _vehicleRepository;
    private readonly Mock<IManufacturerRepository> _manufacturerRepository;
    private readonly Mock<IVehicleCategoryRepository> _categoryRepository;
    private readonly VehicleService _service;

    public VehicleServiceTests()
    {
        _vehicleRepository = new Mock<IVehicleRepository>();
        _manufacturerRepository = new Mock<IManufacturerRepository>();
        _categoryRepository = new Mock<IVehicleCategoryRepository>();

        _service = new VehicleService(
            _vehicleRepository.Object,
            _manufacturerRepository.Object,
            _categoryRepository.Object);
    }

    #region Helpers

    private static VehicleCategory CreateMediumCategory() =>
        new()
        {
            Id = 2,
            Name = "Medium",
            MinWeight = 500,
            MaxWeight = 2500
        };

    #endregion

    #region GetDetails

    [Fact]
    public void GetDetails_WhenVehicleExists_ReturnsVehicleDetails()
    {
        // Arrange
        var vehicle = new Vehicle
        {
            Id = 1,
            OwnerName = "John",
            ManufacturerId = 1,
            YearOfManufacture = 2020,
            Weight = 1200,
            CategoryId = 2,
            Manufacturer = new Manufacturer { Id = 1, Name = "Toyota" },
            Category = new VehicleCategory
            {
                Id = 2,
                Name = "Medium",
                Icon = "🚙"
            }
        };

        _vehicleRepository.Setup(r => r.GetVehicleDetailsById(1)).Returns(vehicle);

        // Act
        var result = _service.GetDetails(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("John", result.OwnerName);
        Assert.Equal("Toyota", result.ManufacturerName);
        Assert.Equal(2020, result.YearOfManufacture);
        Assert.Equal(1200, result.WeightKg);
        Assert.Equal("Medium", result.CategoryName);
        Assert.Equal("🚙", result.CategoryIcon);
    }

    [Fact]
    public void GetDetails_WhenVehicleDoesNotExist_ReturnsNull()
    {
        // Arrange
        _vehicleRepository.Setup(r => r.GetVehicleDetailsById(999)).Returns((Vehicle?)null);

        // Act
        var result = _service.GetDetails(999);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Create

    [Fact]
    public void Create_WhenManufacturerAndCategoryAreValid_CreatesVehicle()
    {
        // Arrange
        var model = new VehicleFormViewModel
        {
            OwnerName = "John",
            ManufacturerId = 1,
            YearOfManufacture = 2020,
            Weight = 1200
        };

        _vehicleRepository.Setup(r => r.HasManufacturer(1)).Returns(true);
        _categoryRepository
            .Setup(r => r.GetVehicleCategoryByWeight(1200))
            .Returns(CreateMediumCategory());

        // Act
        var result = _service.Create(model);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);

        _vehicleRepository.Verify(
            r => r.Add(It.Is<Vehicle>(v =>
                v.OwnerName == "John" &&
                v.ManufacturerId == 1 &&
                v.YearOfManufacture == 2020 &&
                v.Weight == 1200 &&
                v.CategoryId == 2 &&
                !v.IsDeleted)),
            Times.Once);
    }

    [Fact]
    public void Create_WhenManufacturerDoesNotExist_ReturnsError()
    {
        // Arrange
        var model = new VehicleFormViewModel
        {
            OwnerName = "John",
            ManufacturerId = 99,
            YearOfManufacture = 2020,
            Weight = 1200
        };

        _vehicleRepository.Setup(r => r.HasManufacturer(99)).Returns(false);

        // Act
        var result = _service.Create(model);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("The selected manufacturer does not exist.", result.ErrorMessage);

        _vehicleRepository.Verify(r => r.Add(It.IsAny<Vehicle>()), Times.Never);
        _categoryRepository.Verify(
            r => r.GetVehicleCategoryByWeight(It.IsAny<decimal>()),
            Times.Never);
    }

    [Fact]
    public void Create_WhenWeightDoesNotBelongToCategory_ReturnsError()
    {
        // Arrange
        var model = new VehicleFormViewModel
        {
            OwnerName = "John",
            ManufacturerId = 1,
            YearOfManufacture = 2020,
            Weight = 3500
        };

        _vehicleRepository.Setup(r => r.HasManufacturer(1)).Returns(true);
        _categoryRepository
            .Setup(r => r.GetVehicleCategoryByWeight(3500))
            .Returns((VehicleCategory?)null);

        // Act
        var result = _service.Create(model);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(
            "No vehicle category covers this weight. Check the category ranges.",
            result.ErrorMessage);

        _vehicleRepository.Verify(r => r.Add(It.IsAny<Vehicle>()), Times.Never);
    }

    #endregion

    #region Update

    [Fact]
    public void Update_WhenVehicleExists_UpdatesVehicleAndRecalculatesCategory()
    {
        // Arrange
        var existingVehicle = new Vehicle
        {
            Id = 1,
            OwnerName = "Old Owner",
            ManufacturerId = 1,
            YearOfManufacture = 2020,
            Weight = 400,
            CategoryId = 1
        };

        var model = new VehicleFormViewModel
        {
            Id = 1,
            OwnerName = "New Owner",
            ManufacturerId = 1,
            YearOfManufacture = 2022,
            Weight = 1200
        };

        _vehicleRepository.Setup(r => r.GetVehicleById(1)).Returns(existingVehicle);
        _vehicleRepository.Setup(r => r.HasManufacturer(1)).Returns(true);
        _categoryRepository
            .Setup(r => r.GetVehicleCategoryByWeight(1200))
            .Returns(CreateMediumCategory());

        // Act
        var result = _service.Update(model);

        // Assert
        Assert.True(result.Success);

        Assert.Equal("New Owner", existingVehicle.OwnerName);
        Assert.Equal(1, existingVehicle.ManufacturerId);
        Assert.Equal(2022, existingVehicle.YearOfManufacture);
        Assert.Equal(1200, existingVehicle.Weight);

        // Important business rule:
        // category is recalculated when vehicle weight changes.
        Assert.Equal(2, existingVehicle.CategoryId);

        _vehicleRepository.Verify(r => r.Update(existingVehicle), Times.Once);
    }

    [Fact]
    public void Update_WhenVehicleDoesNotExist_ReturnsNotFoundError()
    {
        // Arrange
        var model = new VehicleFormViewModel
        {
            Id = 999,
            OwnerName = "John",
            ManufacturerId = 1,
            YearOfManufacture = 2020,
            Weight = 1200
        };

        _vehicleRepository.Setup(r => r.GetVehicleById(999)).Returns((Vehicle?)null);

        // Act
        var result = _service.Update(model);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(VehicleNotFoundMessage, result.ErrorMessage);

        _vehicleRepository.Verify(r => r.Update(It.IsAny<Vehicle>()), Times.Never);
    }

    #endregion

    #region Delete

    [Fact]
    public void Delete_WhenVehicleExists_SoftDeletesVehicle()
    {
        // Arrange
        var vehicle = new Vehicle
        {
            Id = 1,
            OwnerName = "John",
            IsDeleted = false
        };

        _vehicleRepository.Setup(r => r.GetVehicleById(1)).Returns(vehicle);

        // Act
        var result = _service.Delete(1);

        // Assert
        Assert.True(result.Success);
        Assert.True(vehicle.IsDeleted);

        _vehicleRepository.Verify(r => r.Update(vehicle), Times.Once);
    }

    [Fact]
    public void Delete_WhenVehicleDoesNotExist_ReturnsNotFoundError()
    {
        // Arrange
        _vehicleRepository.Setup(r => r.GetVehicleById(999)).Returns((Vehicle?)null);

        // Act
        var result = _service.Delete(999);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(VehicleNotFoundMessage, result.ErrorMessage);

        _vehicleRepository.Verify(r => r.Update(It.IsAny<Vehicle>()), Times.Never);
    }

    #endregion

    #region GetVehicles

    [Fact]
    public void GetVehicles_ReturnsMappedVehicleList()
    {
        // Arrange
        var summaries = new List<VehicleSummary>
        {
            new(1, "John", "Toyota", 2020, 1200, "Medium", "🚙"),
            new(2, "David", "Mazda", 2022, 450, "Light", "🚗")
        };

        _vehicleRepository.Setup(r => r.GetVehicleCount(null)).Returns(2);
        _vehicleRepository
            .Setup(r => r.GetPageWiseVehicleDetails(null, 1, 10, "owner", "asc"))
            .Returns(summaries);

        // Act
        var result = _service.GetVehicles(null, 1, 10, "owner", "asc");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalItems);
        Assert.Equal(1, result.CurrentPage);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(2, result.Vehicles.Count);

        Assert.Equal("John", result.Vehicles[0].OwnerName);
        Assert.Equal("Toyota", result.Vehicles[0].ManufacturerName);
        Assert.Equal("Medium", result.Vehicles[0].CategoryName);

        Assert.Equal("David", result.Vehicles[1].OwnerName);
        Assert.Equal("Mazda", result.Vehicles[1].ManufacturerName);
        Assert.Equal("Light", result.Vehicles[1].CategoryName);
    }

    [Fact]
    public void GetVehicles_WhenPageSizeIsZero_UsesDefaultPageSize()
    {
        // Arrange
        _vehicleRepository.Setup(r => r.GetVehicleCount(null)).Returns(0);
        _vehicleRepository
            .Setup(r => r.GetPageWiseVehicleDetails(null, 1, 10, "owner", "asc"))
            .Returns(new List<VehicleSummary>());

        // Act
        var result = _service.GetVehicles(null, 1, 0, "owner", "asc");

        // Assert
        Assert.Equal(10, result.PageSize);

        _vehicleRepository.Verify(
            r => r.GetPageWiseVehicleDetails(null, 1, 10, "owner", "asc"),
            Times.Once);
    }

    #endregion

    #region Form View Models

    [Fact]
    public void GetEditViewModel_WhenVehicleExists_ReturnsMappedModel()
    {
        // Arrange
        var vehicle = new Vehicle
        {
            Id = 1,
            OwnerName = "John",
            ManufacturerId = 2,
            YearOfManufacture = 2021,
            Weight = 1500
        };

        _vehicleRepository.Setup(r => r.GetVehicleDetailsById(1)).Returns(vehicle);
        _manufacturerRepository
            .Setup(r => r.GetAllManufacturer())
            .Returns(new List<Manufacturer>
            {
                new() { Id = 1, Name = "Toyota" },
                new() { Id = 2, Name = "Mazda" }
            });

        // Act
        var result = _service.GetEditViewModel(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("John", result.OwnerName);
        Assert.Equal(2, result.ManufacturerId);
        Assert.Equal(2021, result.YearOfManufacture);
        Assert.Equal(1500, result.Weight);
        Assert.Equal(2, result.Manufacturers.Count());
    }

    [Fact]
    public void GetEditViewModel_WhenVehicleDoesNotExist_ReturnsNull()
    {
        // Arrange
        _vehicleRepository.Setup(r => r.GetVehicleDetailsById(999)).Returns((Vehicle?)null);

        // Act
        var result = _service.GetEditViewModel(999);

        // Assert
        Assert.Null(result);
    }

    #endregion
}