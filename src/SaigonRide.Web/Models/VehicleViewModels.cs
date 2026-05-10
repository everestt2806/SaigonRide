using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SaigonRide.Domain.Entities;
using SaigonRide.Domain.Enums;
using SaigonRide.Services.Inventory;

namespace SaigonRide.Web.Models;

public class VehicleListViewModel
{
    public PagedResult<Vehicle> Page { get; set; } = new(Array.Empty<Vehicle>(), 0, 1, 20);
    public VehicleStatus? Status { get; set; }
    public int? StationId { get; set; }
    public string? Search { get; set; }
    public IEnumerable<SelectListItem> StationOptions { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> StatusOptions { get; set; } = Array.Empty<SelectListItem>();
}

public class VehicleEditViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(20, MinimumLength = 4)]
    [Display(Name = "License Plate")]
    public string LicensePlate { get; set; } = string.Empty;

    [Required, Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
    [Display(Name = "Category")]
    public int VehicleCategoryId { get; set; }

    [Required, Range(1, int.MaxValue, ErrorMessage = "Choose a station.")]
    [Display(Name = "Home Station")]
    public int HomeStationId { get; set; }

    [Required]
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;

    public byte[]? RowVersion { get; set; }

    public IEnumerable<SelectListItem> CategoryOptions { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> StationOptions { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> StatusOptions { get; set; } = Array.Empty<SelectListItem>();
}

public class VehicleDecommissionViewModel
{
    public int Id { get; set; }
    public string LicensePlate { get; set; } = string.Empty;

    [Required, StringLength(255, MinimumLength = 5, ErrorMessage = "Provide a reason (5–255 characters).")]
    public string Reason { get; set; } = string.Empty;
}
