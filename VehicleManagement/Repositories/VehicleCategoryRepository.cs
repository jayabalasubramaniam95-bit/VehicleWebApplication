using Microsoft.EntityFrameworkCore;
using VehicleManagement.Data;
using VehicleManagement.Models;

namespace VehicleManagement.Repositories;

public class VehicleCategoryRepository : IVehicleCategoryRepository
{
    #region Dependencies & Constructor

    private readonly ApplicationDbContext _context;

    public VehicleCategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    private IQueryable<VehicleCategory> Active =>
        _context.VehicleCategories.Where(c => !c.IsDeleted);

    #endregion

    #region Queries

    public List<VehicleCategorySummary> GetVehicleCategorySummaries() =>
        Active
            .AsNoTracking()
            .OrderBy(c => c.MinWeight)
            .Select(c => new VehicleCategorySummary(
                c.Id,
                c.Name,
                c.Icon,
                c.MinWeight,
                c.MaxWeight,
                _context.Vehicles.Where(x=>!x.IsDeleted).Count(v => v.CategoryId == c.Id)))
            .ToList();

    public List<VehicleCategory> GetAllVehicleCategory() =>
        Active
            .OrderBy(c => c.MinWeight)
            .ToList();

    public VehicleCategory? GetVehicleCategoryById(int id) =>
        Active
            .AsNoTracking()
            .FirstOrDefault(c => c.Id == id);

    public VehicleCategory? GetVehicleCategoryByWeight(decimal weight) =>
        Active
            .AsNoTracking()
            .FirstOrDefault(c =>
                weight >= c.MinWeight &&
                (c.MaxWeight == null || weight < c.MaxWeight));

    #endregion

    #region Existence Checks

    public bool IsVehicleCategoryNameExists(string name, int? excludeId = null)
    {
        var normalized = name.Trim().ToLower();

        return Active.Any(c =>
            c.Name.ToLower() == normalized &&
            (excludeId == null || c.Id != excludeId));
    }

    public bool HasVehicles(int categoryId) =>
        _context.Vehicles.Any(v => v.CategoryId == categoryId && !v.IsDeleted);

    #endregion

    #region Commands

    public void Add(VehicleCategory category)
    {
        _context.VehicleCategories.Add(category);
        _context.SaveChanges();
    }
    public void Update(VehicleCategory category)
    {
        _context.VehicleCategories.Update(category);
        _context.SaveChanges();
    }

    public void InTransaction(Action work)
    {
        using var transaction = _context.Database.BeginTransaction();
        work();
        transaction.Commit();   // an exception in work() skips this, so the transaction rolls back
    }

    #endregion
}
