using Microsoft.EntityFrameworkCore;
using VehicleManagement.Data;
using VehicleManagement.Models;

namespace VehicleManagement.Repositories;

public class VehicleRepository : IVehicleRepository
{
    #region Dependencies & Constructor

    private readonly ApplicationDbContext _context;

    public VehicleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // Every read starts here, so deleted vehicles (or vehicles of deleted manufacturers) never leak in
    private IQueryable<Vehicle> Active =>
        _context.Vehicles.Where(v => !v.IsDeleted && !v.Manufacturer.IsDeleted && !v.Category.IsDeleted);

    #endregion

    #region Queries

    public List<Vehicle> GetAllVehicles() =>
        Active
            .OrderBy(v => v.OwnerName)
            .ToList();

    public Vehicle? GetVehicleById(int id) =>
        Active.FirstOrDefault(v => v.Id == id);

    public Vehicle? GetVehicleDetailsById(int id) =>
        Active
            .AsNoTracking()
            .Include(m => m.Manufacturer)
            .Include(v => v.Category)
            .FirstOrDefault(v => v.Id == id);

    public List<VehicleSummary> GetPageWiseVehicleDetails(string? search, int pageNumber, int pageSize, string sortColumn, string sortDirection)
    {
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Max(pageSize, 1);

         var query = ApplySearch(Active, search)
        .AsNoTracking();

        var isDescending =
            string.Equals(
                sortDirection,
                "desc",
                StringComparison.OrdinalIgnoreCase);
        query = sortColumn.ToLowerInvariant() switch
        {
            "manufacturer" => isDescending
                ? query.OrderByDescending(v => v.Manufacturer.Name)
                    .ThenByDescending(v => v.Id)
                : query.OrderBy(v => v.Manufacturer.Name)
                    .ThenBy(v => v.Id),

            "year" => isDescending
                ? query.OrderByDescending(v => v.YearOfManufacture)
                    .ThenByDescending(v => v.Id)
                : query.OrderBy(v => v.YearOfManufacture)
                    .ThenBy(v => v.Id),

            "weight" => isDescending
                ? query.OrderByDescending(v => v.Weight)
                    .ThenByDescending(v => v.Id)
                : query.OrderBy(v => v.Weight)
                    .ThenBy(v => v.Id),

                // Default = Owner
                _ => isDescending
                    ? query.OrderByDescending(v => v.OwnerName)
                        .ThenByDescending(v => v.Id)
                    : query.OrderBy(v => v.OwnerName)
                        .ThenBy(v => v.Id)
        }; 

         return query
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .Select(v => new VehicleSummary(
            v.Id,
            v.OwnerName,
            v.Manufacturer.Name,
            v.YearOfManufacture,
            v.Weight,
            v.Category != null ? v.Category.Name : null,
            v.Category != null ? v.Category.Icon : null))
        .ToList();
    }

    // Uses the same base query as GetPaged, so the count always matches the rows
    public int GetVehicleCount(string? search) =>
        ApplySearch(Active, search).Count();

    #endregion

    #region Existence Checks

    public bool HasManufacturer(int manufacturerId) =>
        _context.Manufacturers.Any(m => !m.IsDeleted && m.Id == manufacturerId);

    #endregion

    #region Commands

    public void Add(Vehicle vehicle)
    {
    _context.Vehicles.Add(vehicle);
    _context.SaveChanges();
    }
    public void Update(Vehicle vehicle)
    {
        _context.Vehicles.Update(vehicle);
         _context.SaveChanges();
    }

    #endregion

    #region Helpers

    private static IQueryable<Vehicle> ApplySearch(IQueryable<Vehicle> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;

        var term = search.Trim();
        query = query.Where(x => !x.IsDeleted && !x.Manufacturer.IsDeleted && !x.Category.IsDeleted);

        return query.Where(v =>
            v.OwnerName.Contains(term) ||
            v.Manufacturer.Name.Contains(term) ||
            (v.Category != null && v.Category.Name.Contains(term)));
    }

    #endregion
}
