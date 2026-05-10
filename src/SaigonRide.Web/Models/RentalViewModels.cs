using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SaigonRide.Domain.Entities;
using SaigonRide.Domain.Enums;
using SaigonRide.Domain.ValueObjects;

namespace SaigonRide.Web.Models;

public class ActiveRentalViewModel
{
    public Rental Rental { get; set; } = default!;
    public IEnumerable<SelectListItem> ReturnStationOptions { get; set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> PaymentMethodOptions { get; set; } = Array.Empty<SelectListItem>();
}

public class EndRentalRequestViewModel
{
    [Required]
    public int RentalId { get; set; }

    [Required, Range(1, int.MaxValue, ErrorMessage = "Choose a return station.")]
    public int ReturnStationId { get; set; }

    [Required]
    public PaymentMethod PaymentMethod { get; set; }
}

public class CheckoutPreviewViewModel
{
    public Rental Rental { get; set; } = default!;
    public FareBreakdown Fare { get; set; } = default!;
    public int ReturnStationId { get; set; }
    public string ReturnStationName { get; set; } = string.Empty;
    public IEnumerable<SelectListItem> PaymentMethodOptions { get; set; } = Array.Empty<SelectListItem>();
}

public class ReceiptViewModel
{
    public Rental Rental { get; set; } = default!;
    public FareBreakdown Fare { get; set; } = default!;
    public Transaction Transaction { get; set; } = default!;
    public string? MaskedPassport { get; set; }
}
