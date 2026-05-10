using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SaigonRide.Data.Repositories;
using SaigonRide.Domain.Enums;
using SaigonRide.Services.Inventory;
using SaigonRide.Services.Payment;
using SaigonRide.Services.Rentals;
using SaigonRide.Web.Infrastructure;
using SaigonRide.Web.Models;

namespace SaigonRide.Web.Controllers;

/// <summary>
/// UC-02 Process Rental & Calculate Fares (owner: Minh). Wraps the rental
/// lifecycle into REST-style POST endpoints. Each mutation routes through
/// <see cref="IRentalService"/> which guarantees the EF transaction
/// boundaries and audit logging (NFR-06).
/// </summary>
[Authorize]
public class RentalController : Controller
{
    private readonly IRentalService _rentalService;
    private readonly IStationService _stationService;
    private readonly IPaymentService _paymentService;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<RentalController> _logger;

    public RentalController(
        IRentalService rentalService,
        IStationService stationService,
        IPaymentService paymentService,
        IUserRepository userRepository,
        ICurrentUser currentUser,
        ILogger<RentalController> logger)
    {
        _rentalService = rentalService;
        _stationService = stationService;
        _paymentService = paymentService;
        _userRepository = userRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int vehicleId)
    {
        var userId = _currentUser.Id ?? 0;
        var result = await _rentalService.StartRentalAsync(userId, vehicleId);
        if (!result.Success)
        {
            TempData["error"] = result.ErrorMessage;
            return RedirectToAction("Index", "Station");
        }
        TempData["success"] = "Rental started. Have a safe ride!";
        return RedirectToAction(nameof(Active));
    }

    [HttpGet("rentals/active")]
    public async Task<IActionResult> Active()
    {
        var userId = _currentUser.Id ?? 0;
        var rental = await _rentalService.GetActiveAsync(userId);
        if (rental is null)
        {
            TempData["info"] = "You do not have an active rental.";
            return RedirectToAction("Index", "Station");
        }
        var fullRental = await _rentalService.GetByIdForUserAsync(rental.Id, userId);
        var stations = await _stationService.ListActiveAsync();
        var vm = new ActiveRentalViewModel
        {
            Rental = fullRental ?? rental,
            ReturnStationOptions = stations.Select(s =>
                new SelectListItem($"{s.Name} ({s.CurrentCount}/{s.Capacity})", s.Id.ToString())),
            PaymentMethodOptions = await BuildPaymentOptionsAsync(userId)
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewFare(int rentalId, int returnStationId)
    {
        var userId = _currentUser.Id ?? 0;
        var rental = await _rentalService.GetByIdForUserAsync(rentalId, userId);
        if (rental is null) return NotFound();
        var fareResult = await _rentalService.PreviewFareAsync(rentalId, returnStationId);
        if (!fareResult.Success)
        {
            TempData["error"] = fareResult.ErrorMessage;
            return RedirectToAction(nameof(Active));
        }
        var station = await _stationService.GetByIdAsync(returnStationId);
        var vm = new CheckoutPreviewViewModel
        {
            Rental = rental,
            Fare = fareResult.Value!,
            ReturnStationId = returnStationId,
            ReturnStationName = station?.Name ?? "",
            PaymentMethodOptions = await BuildPaymentOptionsAsync(userId)
        };
        return View("Checkout", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> End(EndRentalRequestViewModel vm)
    {
        if (!ModelState.IsValid) return RedirectToAction(nameof(Active));
        var userId = _currentUser.Id ?? 0;

        var user = await _userRepository.GetByIdWithDetailsAsync(userId);
        if (user is null) return Unauthorized();
        if (!_paymentService.IsAllowedFor(user.UserType, vm.PaymentMethod))
        {
            TempData["error"] = $"Payment method {vm.PaymentMethod} is not available for your account type.";
            return RedirectToAction(nameof(Active));
        }

        // Server-side: block VNPay if fare < 10,000 VND (VNPay sandbox minimum)
        if (vm.PaymentMethod == PaymentMethod.VNPay)
        {
            var farePreview = await _rentalService.PreviewFareAsync(vm.RentalId, vm.ReturnStationId);
            if (farePreview.Success && farePreview.Value!.TotalFare < 10000)
            {
                TempData["error"] = $"VNPay requires a minimum of 10,000 ₫. Your fare is {farePreview.Value.TotalFare:N0} ₫. Please ride longer or choose Cash.";
                return RedirectToAction(nameof(Active));
            }
        }

        var input = new EndRentalInput(vm.RentalId, userId, vm.ReturnStationId, vm.PaymentMethod);
        var result = await _rentalService.EndRentalAsync(input);
        if (!result.Success)
        {
            TempData["error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Active));
        }

        // If VNPay returned a payment URL, redirect the user to the VNPay sandbox page
        if (!string.IsNullOrEmpty(result.Value!.PaymentUrl))
        {
            return Redirect(result.Value.PaymentUrl);
        }

        return RedirectToAction(nameof(Receipt), new { id = result.Value!.Rental.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int rentalId)
    {
        var userId = _currentUser.Id ?? 0;
        var result = await _rentalService.CancelRentalAsync(rentalId, userId);
        if (!result.Success)
        {
            TempData["error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Active));
        }
        TempData["success"] = "Rental cancelled within the free window. No charge.";
        return RedirectToAction(nameof(History));
    }

    [HttpGet("rentals/history")]
    [HttpGet("rentals/history/{page:int?}")]
    public async Task<IActionResult> History(int page = 1, int pageSize = 10)
    {
        var userId = _currentUser.Id ?? 0;
        var result = await _rentalService.GetHistoryPagedAsync(userId, page, pageSize);
        ViewBag.CurrentPage = result.Page;
        ViewBag.TotalPages = result.TotalPages;
        ViewBag.PageSize = pageSize;
        return View(result.Items);
    }

    [HttpGet]
    public async Task<IActionResult> Receipt(int id)
    {
        var userId = _currentUser.Id ?? 0;
        var rental = await _rentalService.GetByIdForUserAsync(id, userId);
        if (rental is null) return NotFound();
        if (rental.Transaction is null)
        {
            TempData["info"] = "This rental does not have a paid transaction yet.";
            return RedirectToAction(nameof(History));
        }

        var fare = new SaigonRide.Domain.ValueObjects.FareBreakdown(
            rental.DurationMinutes ?? 0,
            rental.RatePerMinSnapshot,
            rental.BaseFare ?? 0m,
            rental.Discount ?? 0m,
            rental.TotalFare ?? 0m,
            (rental.Discount ?? 0m) > 0m,
            rental.ReturnStation?.OccupancyPct ?? 0m);

        var user = await _userRepository.GetByIdWithDetailsAsync(userId);
        return View(new ReceiptViewModel
        {
            Rental = rental,
            Fare = fare,
            Transaction = rental.Transaction,
            MaskedPassport = user?.TouristDetails?.GetMaskedPassport()
        });
    }

    private async Task<IEnumerable<SelectListItem>> BuildPaymentOptionsAsync(int userId)
    {
        var user = await _userRepository.GetByIdWithDetailsAsync(userId);
        if (user is null) return Array.Empty<SelectListItem>();
        return _paymentService.GetAllowedMethods(user.UserType)
            .Select(m => new SelectListItem(m.ToString(), m.ToString()));
    }
}
