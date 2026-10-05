using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VehicleManagement.ViewModels;

/// <summary>
/// One view model for both Create and Edit (Id == 0 means "new vehicle").
/// The category is NOT part of the form: it is derived from the weight when the vehicle is saved.
/// Fields are nullable so an untouched input gives a clear "required" message instead of a binding error.
/// </summary>
public class VehicleFormViewModel : IValidatableObject
{
    public const int MinYear = 1886;
    public const int OwnerNameMaxLength = 100;

    private string _ownerName = string.Empty;

    public int Id { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(OwnerNameMaxLength, MinimumLength = 2,
        ErrorMessage = "{0} must be between {2} and {1} characters.")]
    [Display(Name = "Owner Name")]
    public string OwnerName
    {
        get => _ownerName;
        set => _ownerName = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Please select a manufacturer.")]
    [Display(Name = "Manufacturer")]
    public int? ManufacturerId { get; set; }



    [Required(ErrorMessage = "{0} is required.")]
    [Range(MinYear, 2026, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Year of Manufacture")]
    public int? YearOfManufacture { get; set; }

    [Required(ErrorMessage = "{0} is required.")]
    [Range(0.01, 999999.99, ErrorMessage = "{0} must be greater than 0 and at most {2}.")]
    [Display(Name = "Weight (kg)")]
    public decimal? Weight { get; set; }

    public IEnumerable<SelectListItem> Manufacturers { get; set; } = new List<SelectListItem>();

    public bool IsEdit => Id > 0;

    // Rules that attributes cannot express
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var latestYear = DateTime.UtcNow.Year + 1;   // allow next model year

        if (YearOfManufacture > latestYear)
            yield return new ValidationResult(
                $"Year of manufacture cannot be later than {latestYear}.", new[] { nameof(YearOfManufacture) });

        if (Weight is { } weight && decimal.Round(weight, 2) != weight)
            yield return new ValidationResult(
                "Weight can have at most 2 decimal places.", new[] { nameof(Weight) });
    }
}
