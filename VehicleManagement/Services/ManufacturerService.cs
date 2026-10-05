using Microsoft.EntityFrameworkCore;
using VehicleManagement.Data;      // adjust to your DbContext namespace
using VehicleManagement.Models;    // adjust to your entity namespace
using VehicleManagement.Repositories;
using VehicleManagement.ViewModels;

namespace VehicleManagement.Services;

public class ManufacturerService : IManufacturerService
{
    #region Constants
    private const int DefaultPageSize = 10;
    #endregion
    IManufacturerRepository _manufacturerRepository;
    public ManufacturerService(IManufacturerRepository manufacturerRepository)
    {
        _manufacturerRepository=manufacturerRepository;
    }

    public  ManufacturerListViewModel GetPageWiseManufacturer(
        string? search, int page, int pageSize)
    {
        if (pageSize <= 0) pageSize = DefaultPageSize;
                search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var totalItems = _manufacturerRepository.CountManufacturer(search);
        if(totalItems == 0){ return new ManufacturerListViewModel();}
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = _manufacturerRepository
            .GetPageWiseManufacturer(search, page, pageSize)
            .Select(m => new ManufacturerListItemViewModel
            {
                Id = m.Id,
                Name = m.Name,
                IsDefault = m.IsDefault,
                VehicleCount = m.VehicleCount
            })
            .ToList();

        return new ManufacturerListViewModel
        {
            Manufacturers = items,
            Search = search,
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };
    }

    public ManufacturerDetailsViewModel? GetManufacturerDetails(int id)
    {
        var manufacturer = _manufacturerRepository.GetManufacturersWithVehicles(id);
            return manufacturer is null? null :
                 new ManufacturerDetailsViewModel
                {
                    Id = manufacturer.Id,
                    Name = manufacturer.Name,
                    IsDefault = manufacturer.IsDefault,
                    VehicleCount = manufacturer.Vehicles.Count,
                    Vehicles = manufacturer.Vehicles
                        .Select(v => new VehicleListItemViewModel
                        {
                            Id = v.Id,
                            OwnerName = v.OwnerName,
                            ManufacturerName = v.Manufacturer.Name,
                            YearOfManufacture = v.YearOfManufacture,
                            CategoryName = v.Category?.Name ?? string.Empty,
                            CategoryIcon = v.Category?.Icon ?? string.Empty,
                            Weight = v.Weight
                        })
                        .ToList()
                };
        }


    public ManufacturerFormViewModel? GetManufacturerForEdit(int id)
    {
        var manufacturer = _manufacturerRepository.GetManufacturersWithVehicles(id);
        return manufacturer is null? null : new ManufacturerFormViewModel { Id = manufacturer.Id, Name = manufacturer.Name };
    }
    public bool IsManufacturersNameExists(string name, int? excludeId = null)
    {
        return _manufacturerRepository.IsManufacturersNameExists(name, excludeId);
    }

    public  void Create(ManufacturerFormViewModel model)
    {
        _manufacturerRepository.Add(new Manufacturer { Name = model.Name, IsDefault = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    }

    public  bool Update(ManufacturerFormViewModel model)
    {
        var entity = _manufacturerRepository.GetManufacturersById(model.Id);
        if (entity is null) return false;
        entity.Name = model.Name;
        entity.UpdatedAt = DateTime.UtcNow;
        _manufacturerRepository.Update(entity);
        return true;
    }

    public  DeleteResult Delete(int id)
    {
        var entity =  _manufacturerRepository.GetManufacturersById(id);
        if (entity is null) return DeleteResult.NotFound;
        if (entity.IsDefault) return DeleteResult.IsDefault;
        if (_manufacturerRepository.IsManufacturersHasVehicles(id)) return DeleteResult.HasVehicles;
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        _manufacturerRepository.Update(entity);
        return DeleteResult.Deleted;
    }
}
