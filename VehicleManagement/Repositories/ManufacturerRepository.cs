using Microsoft.EntityFrameworkCore;
using VehicleManagement.Data;
using VehicleManagement.Models;

namespace VehicleManagement.Repositories;

public class ManufacturerRepository : IManufacturerRepository
{
    private readonly ApplicationDbContext _context;

    public ManufacturerRepository(ApplicationDbContext context) => _context = context;

    public int CountManufacturer(string? search) =>
        SearchFilter(search).Count();

    public List<ManufacturerSummary> GetPageWiseManufacturer(
        string? search, int pageNumber, int pageSize)
    {
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Max(pageSize, 1);

        var query = SearchFilter(search);
        return query.AsNoTracking()
            .OrderBy(v => v.Name)
            .ThenBy(v => v.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new ManufacturerSummary(m.Id, m.Name, m.IsDefault,  m.Vehicles.Count(v => !v.IsDeleted)))
            .ToList();
    }

    public Manufacturer? GetManufacturersWithVehicles(int id) =>
        _context.Manufacturers
           .AsNoTracking()
           .Include(m => m.Vehicles.Where(v => !v.IsDeleted && !v.Category.IsDeleted))
           .ThenInclude(v => v.Category)
           .FirstOrDefault(m => m.Id == id && !m.IsDeleted);

    public Manufacturer? GetManufacturersById(int id) =>
        _context.Manufacturers .AsNoTracking().FirstOrDefault(m => m.Id == id && !m.IsDeleted);

    public bool IsManufacturersNameExists(string name, int? excludeId = null)
    {
        var normalized = name.Trim().ToLower();
        return _context.Manufacturers.Any(m => m.Name.ToLower() == normalized && m.Id != excludeId && !m.IsDeleted);
    }

    public List<Manufacturer> GetAllManufacturer()
    {
        return _context.Manufacturers.Where(m=>!m.IsDeleted).AsNoTracking().OrderBy(m => m.Name).ToList();
    }

    public bool IsManufacturersHasVehicles(int id) =>
        _context.Vehicles.Any(v => v.ManufacturerId == id && !v.IsDeleted);

    public void Add(Manufacturer manufacturer)
    {
        _context.Manufacturers.Add(manufacturer);
        _context.SaveChanges();
    }
    public void Update(Manufacturer manufacturer)
    {
        _context.Manufacturers.Update(manufacturer);
        _context.SaveChanges();
    }
    private IQueryable<Manufacturer> SearchFilter(string? search)
    {
        var query = _context.Manufacturers.Where(m => !m.IsDeleted).AsNoTracking();
        return string.IsNullOrWhiteSpace(search)
            ? query
            : query.Where(m => m.Name.Contains(search.Trim()));
    }
}