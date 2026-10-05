using VehicleManagement.Models;

namespace VehicleManagement.Repositories;

public record VehicleSummary(
    int Id,
    string OwnerName,
    string ManufacturerName,
    int YearOfManufacture,
    decimal Weight,
    string? CategoryName,
    string? CategoryIcon);

public interface IVehicleRepository
{
    #region Queries

    List<VehicleSummary> GetPageWiseVehicleDetails(string? search, int pageNumber, int pageSize, string sortColumn, string sortDirection);
    List<Vehicle> GetAllVehicles();

    Vehicle? GetVehicleById(int id);

    Vehicle? GetVehicleDetailsById(int id);

    int GetVehicleCount(string? search);

    #endregion

    #region Existence Checks

    bool HasManufacturer(int manufacturerId);

    #endregion

    #region Commands

    void Add(Vehicle vehicle);

    void Update(Vehicle vehicle);

    #endregion
}
