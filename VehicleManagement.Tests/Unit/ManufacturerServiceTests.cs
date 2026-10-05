using Moq;
using VehicleManagement.Models;
using VehicleManagement.Repositories;
using VehicleManagement.Services;
using VehicleManagement.ViewModels;

namespace VehicleManagement.Tests.Unit;

public class ManufacturerServiceTests
{
    private readonly Mock<IManufacturerRepository> _repository;
    private readonly ManufacturerService _service;

    public ManufacturerServiceTests()
    {
        _repository = new Mock<IManufacturerRepository>();
        _service = new ManufacturerService(_repository.Object);
    }

    #region List

    [Fact]
    public void GetPageWiseManufacturer_ReturnsMappedManufacturers()
    {
        // Arrange
        var summaries = new List<ManufacturerSummary>
        {
            new(1, "Mazda", true, 3),
            new(2, "Toyota", true, 5)
        };

        _repository.Setup(r => r.CountManufacturer(null)).Returns(2);
        _repository.Setup(r => r.GetPageWiseManufacturer(null, 1, 10)).Returns(summaries);

        // Act
        var result = _service.GetPageWiseManufacturer(null, 1, 10);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalItems);
        Assert.Equal(1, result.CurrentPage);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(2, result.Manufacturers.Count);

        Assert.Equal(1, result.Manufacturers[0].Id);
        Assert.Equal("Mazda", result.Manufacturers[0].Name);
        Assert.True(result.Manufacturers[0].IsDefault);
        Assert.Equal(3, result.Manufacturers[0].VehicleCount);

        Assert.Equal(2, result.Manufacturers[1].Id);
        Assert.Equal("Toyota", result.Manufacturers[1].Name);
        Assert.True(result.Manufacturers[1].IsDefault);
        Assert.Equal(5, result.Manufacturers[1].VehicleCount);
    }

    [Fact]
    public void GetPageWiseManufacturer_WhenPageSizeIsZero_UsesDefaultPageSize()
    {
        // Arrange
        _repository.Setup(r => r.CountManufacturer(null)).Returns(1);
        _repository
            .Setup(r => r.GetPageWiseManufacturer(null, 1, 10))
            .Returns(new List<ManufacturerSummary> { new(1, "Mazda", true, 0) });

        // Act
        var result = _service.GetPageWiseManufacturer(null, 1, 0);

        // Assert
        Assert.Equal(10, result.PageSize);
        _repository.Verify(r => r.GetPageWiseManufacturer(null, 1, 10), Times.Once);
    }

    [Fact]
    public void GetPageWiseManufacturer_TrimsSearchText()
    {
        // Arrange
        _repository.Setup(r => r.CountManufacturer("Mazda")).Returns(1);
        _repository
            .Setup(r => r.GetPageWiseManufacturer("Mazda", 1, 10))
            .Returns(new List<ManufacturerSummary> { new(1, "Mazda", true, 0) });

        // Act
        var result = _service.GetPageWiseManufacturer("   Mazda   ", 1, 10);

        // Assert
        Assert.Equal("Mazda", result.Search);
        _repository.Verify(r => r.CountManufacturer("Mazda"), Times.Once);
    }

    [Fact]
    public void GetPageWiseManufacturer_WhenNoManufacturers_ReturnsEmptyViewModel()
    {
        // Arrange
        _repository.Setup(r => r.CountManufacturer(null)).Returns(0);

        // Act
        var result = _service.GetPageWiseManufacturer(null, 1, 10);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Manufacturers);
    }

    #endregion

    #region Details

    [Fact]
    public void GetManufacturerDetails_WhenManufacturerExists_ReturnsDetails()
    {
        // Arrange
        var manufacturer = new Manufacturer
        {
            Id = 1,
            Name = "Toyota",
            IsDefault = true,
            Vehicles = new List<Vehicle>
            {
                new()
                {
                    Id = 10,
                    OwnerName = "John",
                    YearOfManufacture = 2020,
                    Weight = 1200,
                    Manufacturer = new Manufacturer { Id = 1, Name = "Toyota" },
                    Category = new VehicleCategory
                    {
                        Id = 2,
                        Name = "Medium",
                        Icon = "🚙"
                    }
                }
            }
        };

        _repository.Setup(r => r.GetManufacturersWithVehicles(1)).Returns(manufacturer);

        // Act
        var result = _service.GetManufacturerDetails(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Toyota", result.Name);
        Assert.True(result.IsDefault);
        Assert.Equal(1, result.VehicleCount);
        Assert.Single(result.Vehicles);

        var vehicle = result.Vehicles[0];

        Assert.Equal(10, vehicle.Id);
        Assert.Equal("John", vehicle.OwnerName);
        Assert.Equal("Toyota", vehicle.ManufacturerName);
        Assert.Equal(2020, vehicle.YearOfManufacture);
        Assert.Equal(1200, vehicle.Weight);
        Assert.Equal("Medium", vehicle.CategoryName);
        Assert.Equal("🚙", vehicle.CategoryIcon);
    }

    [Fact]
    public void GetManufacturerDetails_WhenManufacturerDoesNotExist_ReturnsNull()
    {
        // Arrange
        _repository.Setup(r => r.GetManufacturersWithVehicles(999)).Returns((Manufacturer?)null);

        // Act
        var result = _service.GetManufacturerDetails(999);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Edit

    [Fact]
    public void GetManufacturerForEdit_WhenManufacturerExists_ReturnsFormModel()
    {
        // Arrange
        var manufacturer = new Manufacturer { Id = 1, Name = "Toyota" };

        _repository.Setup(r => r.GetManufacturersWithVehicles(1)).Returns(manufacturer);

        // Act
        var result = _service.GetManufacturerForEdit(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Toyota", result.Name);
    }

    [Fact]
    public void GetManufacturerForEdit_WhenManufacturerDoesNotExist_ReturnsNull()
    {
        // Arrange
        _repository.Setup(r => r.GetManufacturersWithVehicles(999)).Returns((Manufacturer?)null);

        // Act
        var result = _service.GetManufacturerForEdit(999);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Name Validation

    [Fact]
    public void IsManufacturersNameExists_DelegatesToRepository()
    {
        // Arrange
        _repository.Setup(r => r.IsManufacturersNameExists("Toyota", null)).Returns(true);

        // Act
        var result = _service.IsManufacturersNameExists("Toyota");

        // Assert
        Assert.True(result);
        _repository.Verify(r => r.IsManufacturersNameExists("Toyota", null), Times.Once);
    }

    [Fact]
    public void IsManufacturersNameExists_WithExcludeId_DelegatesCorrectly()
    {
        // Arrange
        _repository.Setup(r => r.IsManufacturersNameExists("Toyota", 1)).Returns(false);

        // Act
        var result = _service.IsManufacturersNameExists("Toyota", 1);

        // Assert
        Assert.False(result);
        _repository.Verify(r => r.IsManufacturersNameExists("Toyota", 1), Times.Once);
    }

    #endregion

    #region Create

    [Fact]
    public void Create_AddsManufacturer()
    {
        // Arrange
        var model = new ManufacturerFormViewModel { Name = "Tesla" };

        // Act
        _service.Create(model);

        // Assert
        _repository.Verify(
            r => r.Add(It.Is<Manufacturer>(m =>
                m.Name == "Tesla" &&
                !m.IsDefault &&
                !m.IsDeleted)),
            Times.Once);
    }

    #endregion

    #region Update

    [Fact]
    public void Update_WhenManufacturerExists_UpdatesName()
    {
        // Arrange
        var manufacturer = new Manufacturer { Id = 1, Name = "Old Name" };
        var model = new ManufacturerFormViewModel { Id = 1, Name = "New Name" };

        _repository.Setup(r => r.GetManufacturersById(1)).Returns(manufacturer);

        // Act
        var result = _service.Update(model);

        // Assert
        Assert.True(result);
        Assert.Equal("New Name", manufacturer.Name);
        _repository.Verify(r => r.Update(manufacturer), Times.Once);
    }

    [Fact]
    public void Update_WhenManufacturerDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var model = new ManufacturerFormViewModel { Id = 999, Name = "Toyota" };

        _repository.Setup(r => r.GetManufacturersById(999)).Returns((Manufacturer?)null);

        // Act
        var result = _service.Update(model);

        // Assert
        Assert.False(result);
        _repository.Verify(r => r.Update(It.IsAny<Manufacturer>()), Times.Never);
    }

    #endregion

    #region Delete

    [Fact]
    public void Delete_WhenManufacturerDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        _repository.Setup(r => r.GetManufacturersById(999)).Returns((Manufacturer?)null);

        // Act
        var result = _service.Delete(999);

        // Assert
        Assert.Equal(DeleteResult.NotFound, result);
    }

    [Fact]
    public void Delete_WhenManufacturerIsDefault_ReturnsIsDefault()
    {
        // Arrange
        var manufacturer = new Manufacturer
        {
            Id = 1,
            Name = "Toyota",
            IsDefault = true
        };

        _repository.Setup(r => r.GetManufacturersById(1)).Returns(manufacturer);

        // Act
        var result = _service.Delete(1);

        // Assert
        Assert.Equal(DeleteResult.IsDefault, result);
        _repository.Verify(r => r.Update(It.IsAny<Manufacturer>()), Times.Never);
    }

    [Fact]
    public void Delete_WhenManufacturerHasVehicles_ReturnsHasVehicles()
    {
        // Arrange
        var manufacturer = new Manufacturer
        {
            Id = 2,
            Name = "Tesla",
            IsDefault = false
        };

        _repository.Setup(r => r.GetManufacturersById(2)).Returns(manufacturer);
        _repository.Setup(r => r.IsManufacturersHasVehicles(2)).Returns(true);

        // Act
        var result = _service.Delete(2);

        // Assert
        Assert.Equal(DeleteResult.HasVehicles, result);
        _repository.Verify(r => r.Update(It.IsAny<Manufacturer>()), Times.Never);
    }

    [Fact]
    public void Delete_WhenManufacturerCanBeDeleted_SoftDeletesManufacturer()
    {
        // Arrange
        var manufacturer = new Manufacturer
        {
            Id = 2,
            Name = "Tesla",
            IsDefault = false,
            IsDeleted = false
        };

        _repository.Setup(r => r.GetManufacturersById(2)).Returns(manufacturer);
        _repository.Setup(r => r.IsManufacturersHasVehicles(2)).Returns(false);

        // Act
        var result = _service.Delete(2);

        // Assert
        Assert.Equal(DeleteResult.Deleted, result);
        Assert.True(manufacturer.IsDeleted);
        _repository.Verify(r => r.Update(manufacturer), Times.Once);
    }

    #endregion
}