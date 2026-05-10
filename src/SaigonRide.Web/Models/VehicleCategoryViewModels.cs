using System.ComponentModel.DataAnnotations;

namespace SaigonRide.Web.Models;

public class VehicleCategoryViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required, Range(1, 100000, ErrorMessage = "Rate must be between 1 and 100,000 VND.")]
    [Display(Name = "Rate (VND/min)")]
    public decimal RatePerMinVnd { get; set; }
}
