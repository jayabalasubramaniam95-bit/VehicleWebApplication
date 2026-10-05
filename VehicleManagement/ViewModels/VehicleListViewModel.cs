using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace VehicleManagement.ViewModels;

public class VehicleListViewModel
{
    public List<VehicleListItemViewModel> Vehicles { get; init; } = new();

    // Query state (echoed back so pagination links keep the search)
    public string? Search { get; init; }
    public int CurrentPage { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalItems { get; init; }

    // Derived values: never stored, so they cannot get out of sync
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;
    public int FirstItem => TotalItems == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
    public int LastItem => Math.Min(CurrentPage * PageSize, TotalItems);
    public string SortColumn { get; set; } = "owner";
    public string SortDirection { get; set; } = "asc";
}

public class VehicleListItemViewModel
{
    public int Id { get; set; }

    [Display(Name = "Owner")]
    public string OwnerName { get; set; } = string.Empty;

    [Display(Name = "Manufacturer")]
    public string ManufacturerName { get; set; } = string.Empty;

    [Display(Name = "Year")]
    public int YearOfManufacture { get; set; }

    [Display(Name = "Weight (kg)")]
    public decimal Weight { get; set; }

    [Display(Name = "Category")]
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Icon key of the category (see VehicleCategoryIcons).</summary>
    public string? CategoryIcon { get; set; }

    /// <summary>Culture-neutral number so the browser can sort the Weight column numerically.</summary>
    public string WeightSortKey => Weight.ToString(CultureInfo.InvariantCulture);
}
