using System.ComponentModel.DataAnnotations;

namespace VehicleManagement.ViewModels;

public class VehicleDetailsViewModel
{
    public int Id { get; set; }

    [Display(Name = "Owner")]
    public string OwnerName { get; set; } = string.Empty;

    [Display(Name = "Manufacturer")]
    public string ManufacturerName { get; set; } = string.Empty;

    [Display(Name = "Year of Manufacture")]
    public int YearOfManufacture { get; set; }

    [Display(Name = "Weight (kg)")]
    public decimal WeightKg { get; set; }

    [Display(Name = "Category")]
    public string CategoryName { get; set; } = string.Empty;

     public string? CategoryIcon { get; set; }
}
