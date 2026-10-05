using VehicleManagement.Models;
using VehicleManagement.Repositories;
using VehicleManagement.ViewModels;

namespace VehicleManagement.Services;

public class VehicleCategoryService : IVehicleCategoryService
{
    #region Dependencies & Constructor

    private readonly IVehicleRepository _vehicleRepository;
    private readonly IVehicleCategoryRepository _categoryRepository;

    public VehicleCategoryService(
        IVehicleRepository vehicleRepository,
        IVehicleCategoryRepository categoryRepository)
    {
        _vehicleRepository = vehicleRepository;
        _categoryRepository = categoryRepository;
    }

    #endregion

    #region Queries

    public VehicleCategory? GetVehicleCategoryByWeight(decimal weight) =>
        _categoryRepository.GetVehicleCategoryByWeight(weight);

    public VehicleCategoryListViewModel GetVehicleCategoryList() => new()
    {
        Categories = _categoryRepository.GetVehicleCategorySummaries()
            .Select(s => new VehicleCategoryItemViewModel
            {
                Id = s.Id,
                Name = s.Name,
                Icon = s.Icon,
                MinWeight = s.MinWeight,
                MaxWeight = s.MaxWeight,
                VehicleCount = s.VehicleCount
            })
            .ToList()
    };

    public VehicleCategoryFormViewModel? GetVehicleCategoryForEdit(int id)
    {
        var category = _categoryRepository.GetVehicleCategoryById(id);
        if (category is null) return null;

        return new VehicleCategoryFormViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Icon = category.Icon,
            MinWeight = category.MinWeight,
            MaxWeight = category.MaxWeight
        };
    }

    public bool IsVehicleCategoryNameExists(string name, int? excludeId = null) =>
        _categoryRepository.IsVehicleCategoryNameExists(name, excludeId);

    #endregion

    #region Create

    public CategorySaveResult Create(VehicleCategoryFormViewModel model)
    {
        var categories = _categoryRepository.GetAllVehicleCategory();

        var candidate = new VehicleCategory
        {
            Name = model.Name,
            Icon = model.Icon,
            MinWeight = model.MinWeight,
            MaxWeight = model.MaxWeight,
            UpdatedAt = DateTime.UtcNow
        };

        var error = ValidateRanges(categories, candidate);
        if (error is not null) return CategorySaveResult.InvalidRange(error);

        _categoryRepository.InTransaction(() =>
        {
            _categoryRepository.Add(candidate);
            RecategoriseVehicles(categories);
        });

        return CategorySaveResult.Success();
    }

    #endregion

    #region Update

    public CategorySaveResult Update(VehicleCategoryFormViewModel model)
    {
        var categories = _categoryRepository.GetAllVehicleCategory();
        var category = categories.FirstOrDefault(c => c.Id == model.Id);
        if (category is null) return CategorySaveResult.NotFound();

        var oldMin = category.MinWeight;
        var oldMax = category.MaxWeight;
        var now = DateTime.UtcNow;

        category.Name = model.Name;
        category.Icon = model.Icon;
        category.MinWeight = model.MinWeight;
        category.MaxWeight = model.MaxWeight;
        category.UpdatedAt = now;

        var error = ValidateRanges(categories, category);
        if (error is not null) return CategorySaveResult.InvalidRange(error);

        _categoryRepository.InTransaction(() =>
        {
            _categoryRepository.Update(category);
            RecategoriseVehicles(categories);
        });
        return CategorySaveResult.Success();
    }

    #endregion

    #region Delete

    public CategoryDeleteResult Delete(int id)
    {
        var categories = _categoryRepository.GetAllVehicleCategory();
        var category = categories.FirstOrDefault(c => c.Id == id);

        if (category is null) return CategoryDeleteResult.NotFound;
        if (categories.Count == 1) return CategoryDeleteResult.LastCategory;
        if (_categoryRepository.HasVehicles(id)) return CategoryDeleteResult.HasVehicles;

        var now = DateTime.UtcNow;

        // Hand the range to a neighbour so no gap is left behind.
        var previous = categories.FirstOrDefault(c => c.MaxWeight == category.MinWeight);
        if (previous is not null)
        {
            previous.MaxWeight = category.MaxWeight;
            previous.UpdatedAt = now;
        }
        else
        {
            var next = categories.FirstOrDefault(c => c.MinWeight == category.MaxWeight);
            if (next is not null)
            {
                next.MinWeight = category.MinWeight;
                next.UpdatedAt = now;
            }
        }
        category.IsDeleted = true;
        category.UpdatedAt = now;
        _categoryRepository.Update(category);
        return CategoryDeleteResult.Deleted;
    }

    #endregion

    #region Helpers

    private void RecategoriseVehicles(IReadOnlyCollection<VehicleCategory> categories)
    {
        var now = DateTime.UtcNow;

        foreach (var vehicle in _vehicleRepository.GetAllVehicles())
        {
            var match = FindCategory(categories, vehicle.Weight);
            if (match is null || vehicle.CategoryId == match.Id) continue;

            vehicle.CategoryId = match.Id;
            vehicle.UpdatedAt = now;
            _vehicleRepository.Update(vehicle);
        }
    }

    private static VehicleCategory? FindCategory(IEnumerable<VehicleCategory> categories, decimal weight) =>
        categories.FirstOrDefault(c => weight >= c.MinWeight && weight < UpperBound(c));

    private static decimal UpperBound(VehicleCategory c) => c.MaxWeight ?? decimal.MaxValue;

    private static string Describe(VehicleCategory c) => c.MaxWeight is { } max
        ? $"{c.MinWeight:0.##}–{max:0.##} kg"
        : $"{c.MinWeight:0.##} kg and above";

    /// <summary>
    /// Validates the category ranges. When <paramref name="newCategory"/> is supplied (Create),
    /// it first takes its range from an existing category and is added to <paramref name="categories"/>.
    /// Returns an error message, or null when valid.
    /// </summary>
    private string? ValidateRanges(IList<VehicleCategory> categories,
        VehicleCategory? newCategory = null)
    {
        // ───────────── Create only: for new category ─────────────
        if (newCategory is not null && newCategory.Id == 0)
        {

             // e.g. Min 0, Max null -> "Min and max is nothing"
            if ((newCategory.MaxWeight is null || newCategory.MaxWeight == 0m ) &&  newCategory.MinWeight == 0m)
                return $"'{newCategory.Name}' must have at least a minimum or maximum weight.";


            // e.g. Min 3000, Max 2000 -> "Max must be greater than Min"
            if (newCategory.MaxWeight is { } newMax && newMax <= newCategory.MinWeight)
                return $"'{newCategory.Name}' must have a maximum weight greater than its minimum weight.";

            var ordered = categories.OrderBy(c => c.MinWeight).ToList();
            var highest = ordered.LastOrDefault();

            // e.g. highest is 2500–3000, new is 3000–5000 -> continues after it, nothing to shrink
            var continuesAfterHighest = highest?.MaxWeight is { } highestMax
                                        && newCategory.MinWeight == highestMax;

            if (!continuesAfterHighest)
            {
                // e.g. host 2500–∞, new 2500–3000 -> new range sits inside the host
                var host = ordered.FirstOrDefault(c =>
                    newCategory.MinWeight >= c.MinWeight &&
                    UpperBound(newCategory) <= UpperBound(c));

                // e.g. host 0–1000, new 0–1000 -> duplicate range or host 0–1000, new 0–400 
                if (host is null)
                    return $"'{newCategory.Name}' would overlap with an existing category.";
            }
            categories.Add(newCategory);
        }

        // ───────────── Create & Update: validate ─────────────
        var sorted = categories.OrderBy(c => c.MinWeight).ToList();

        // e.g. every category deleted -> list is empty
        if (sorted.Count == 0)
            return "At least one vehicle category is required.";

        // e.g. lowest category starts at 500 kg -> weights 0–500 are uncovered
        if (sorted[0].MinWeight != 0)
            return "The lowest category must start at 0 kg " +
                $"('{sorted[0].Name}' starts at {sorted[0].MinWeight:0.##} kg).";

        for (var i = 0; i < sorted.Count; i++)
        {
            var current = sorted[i];

            // e.g. 1000–1000 or 1500–1000 -> empty/invalid range
            if (current.MaxWeight is { } max && max <= current.MinWeight)
                return $"'{current.Name}' would have an empty or invalid range " +
                    $"({Describe(current)}). Adjust the weights.";

            // Last category may be bounded (2500–3000) or open-ended (2500–∞).
            if (i == sorted.Count - 1) break;

            var next = sorted[i + 1];

            // e.g. current 0–∞ but another category starts at 1000
            if (current.MaxWeight is not { } currentMax)
                return $"'{current.Name}' has no upper limit, but another " +
                    $"category ('{next.Name}') exists after it.";

            // e.g. current 0–1000, next 1200–2000 -> gap 1000–1200
            if (currentMax < next.MinWeight)
                return $"Gap between {currentMax:0.##}–{next.MinWeight:0.##} kg is not allowed.";

            // e.g. current 0–1500, next 1000–2000 -> overlap 1000–1500
            if (currentMax > next.MinWeight)
                return $"Overlap between {next.MinWeight:0.##}–{currentMax:0.##} kg is not allowed.";

        }
        // e.g vehicle weight 3500, vategory range (2500–∞). 
        // suddenly the category is changed to 2500 - 3000, 
        // leaving the vehicle without a category.
        var vehicles = _vehicleRepository.GetAllVehicles();
        if (vehicles.Any(v => FindCategory(sorted, v.Weight) is null))
            return "Some vehicles would not fit into any category. Adjust the ranges.";
        return null;
    }

    #endregion
}