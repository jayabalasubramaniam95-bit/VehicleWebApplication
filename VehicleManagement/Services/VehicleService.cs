using Microsoft.AspNetCore.Mvc.Rendering;
using VehicleManagement.Models;
using VehicleManagement.Repositories;
using VehicleManagement.ViewModels;

namespace VehicleManagement.Services;

public class VehicleService : IVehicleService
{
    #region Constants

    private const int DefaultPageSize = 10;
    private const string Unknown = "Unknown";
    private const string VehicleNotFoundError = "Vehicle could not be found.";
    private const string ManufacturerNotFoundError = "The selected manufacturer does not exist.";
    private const string CategoryNotFoundError = "No vehicle category covers this weight. Check the category ranges.";

    #endregion

    #region Dependencies & Constructor

    private readonly IVehicleRepository _vehicleRepository;
    private readonly IManufacturerRepository _manufacturerRepository;
    private readonly IVehicleCategoryRepository _vehicleCategoryRepository;

    public VehicleService(
        IVehicleRepository vehicleRepository,
        IManufacturerRepository manufacturerRepository,
        IVehicleCategoryRepository vehicleCategoryRepository)
    {
        _vehicleRepository = vehicleRepository;
        _manufacturerRepository = manufacturerRepository;
        _vehicleCategoryRepository = vehicleCategoryRepository;
    }

    #endregion

    #region List (Search, Paging)

    public VehicleListViewModel GetVehicles(string? search, int page, int pageSize)
    {
        if (pageSize <= 0) pageSize = DefaultPageSize;
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var totalItems = _vehicleRepository.GetVehicleCount(search);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = _vehicleRepository
            .GetPageWiseVehicleDetails(search, page, pageSize)
            .Select(v => new VehicleListItemViewModel
            {
                Id = v.Id,
                OwnerName = v.OwnerName,
                ManufacturerName = v.ManufacturerName,
                YearOfManufacture = v.YearOfManufacture,
                Weight = v.Weight,
                CategoryName = v.CategoryName ?? Unknown,
                CategoryIcon = v.CategoryIcon
            })
            .ToList();

        return new VehicleListViewModel
        {
            Vehicles = items,
            Search = search,
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };
    }

    #endregion

    #region Details

    // Returns null when the id does not exist.
    public VehicleDetailsViewModel? GetDetails(int id)
    {
        var vehicle = _vehicleRepository.GetVehicleDetailsById(id);

        return vehicle is null
            ? null
            : new VehicleDetailsViewModel
            {
                Id = vehicle.Id,
                OwnerName = vehicle.OwnerName,
                ManufacturerName = vehicle.Manufacturer?.Name ?? Unknown,
                YearOfManufacture = vehicle.YearOfManufacture,
                WeightKg = vehicle.Weight,
                CategoryName = vehicle.Category?.Name ?? Unknown,
                CategoryIcon = vehicle.Category?.Icon?? string.Empty
            };
    }

    #endregion

    #region Form (Create / Edit)

    public VehicleFormViewModel GetCreateViewModel()
    {
        var model = new VehicleFormViewModel();
        PopulateDropdowns(model);
        return model;
    }

    // Loads the data for the edit form (null when the id does not exist).
    public VehicleFormViewModel? GetEditViewModel(int id)
    {
        var vehicle = _vehicleRepository.GetVehicleDetailsById(id);
        if (vehicle is null) return null;

        var model = new VehicleFormViewModel
        {
            Id = vehicle.Id,
            OwnerName = vehicle.OwnerName,
            ManufacturerId = vehicle.ManufacturerId,
            YearOfManufacture = vehicle.YearOfManufacture,
            Weight = vehicle.Weight
        };

        PopulateDropdowns(model);
        return model;
    }

    public void PopulateDropdowns(VehicleFormViewModel model) =>
        model.Manufacturers = _manufacturerRepository
            .GetAllManufacturer()   // active manufacturers, already ordered by name
            .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Name })
            .ToList();

    // The category is not chosen by the user. It is derived from the weight.
    public ServiceResult Create(VehicleFormViewModel model)
    {
        var check = ResolveReferences(model, out var categoryId);
        if (!check.Success) return check;

        var now = DateTime.UtcNow;
        var vehicle = new Vehicle { CreatedAt = now, IsDeleted = false };

        Apply(vehicle, model, categoryId);
        _vehicleRepository.Add(vehicle);
        return ServiceResult.Ok();
    }

    // The category is re-derived from the weight on every update.
    public ServiceResult Update(VehicleFormViewModel model)
    {
        var vehicle = _vehicleRepository.GetVehicleById(model.Id);
        if (vehicle is null) return ServiceResult.Fail(VehicleNotFoundError);

        var check = ResolveReferences(model, out var categoryId);
        if (!check.Success) return check;

        Apply(vehicle, model, categoryId);
        _vehicleRepository.Update(vehicle);
        return ServiceResult.Ok();
    }

    #endregion

    #region Delete

    public ServiceResult Delete(int id)
    {
        var vehicle = _vehicleRepository.GetVehicleById(id);
        if (vehicle is null) return ServiceResult.Fail(VehicleNotFoundError);

        vehicle.IsDeleted = true;
        vehicle.UpdatedAt = DateTime.UtcNow;
        _vehicleRepository.Update(vehicle);
        return ServiceResult.Ok();
    }

    #endregion

    #region Helpers

    // Checks the manufacturer exists and finds the category whose range contains the weight.
    private ServiceResult ResolveReferences(VehicleFormViewModel model, out int categoryId)
    {
        categoryId = 0;

        if (!_vehicleRepository.HasManufacturer(model.ManufacturerId!.Value))
            return ServiceResult.Fail(ManufacturerNotFoundError);

        var category = _vehicleCategoryRepository.GetVehicleCategoryByWeight(model.Weight!.Value);
        if (category is null)
            return ServiceResult.Fail(CategoryNotFoundError);

        categoryId = category.Id;
        return ServiceResult.Ok();
    }

    // Copies form values onto the entity (validation guarantees the nullable fields have values).
    private static void Apply(Vehicle vehicle, VehicleFormViewModel model, int categoryId)
    {
        vehicle.OwnerName = model.OwnerName;
        vehicle.ManufacturerId = model.ManufacturerId!.Value;
        vehicle.YearOfManufacture = model.YearOfManufacture!.Value;
        vehicle.Weight = model.Weight!.Value;
        vehicle.CategoryId = categoryId;
        vehicle.UpdatedAt = DateTime.UtcNow;
    }

    #endregion
}
