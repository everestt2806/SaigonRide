using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SaigonRide.Domain.Enums;
using SaigonRide.Services.Inventory;
using SaigonRide.Web.Infrastructure;
using SaigonRide.Web.Models;

namespace SaigonRide.Web.Controllers;

/// <summary>
/// UC-01 Manage Vehicle Inventory (owner: Sơn). Strict admin role gate. The
/// controller never touches <see cref="SaigonRideDbContext"/>; all logic lives
/// in <see cref="IVehicleService"/> per NFR-06.
/// </summary>
[Authorize(Roles = "Admin")]
public class VehicleController : Controller
{
    private readonly IVehicleService _vehicleService;
    private readonly IVehicleCategoryService _categoryService;
    private readonly IStationService _stationService;
    private readonly ICurrentUser _currentUser;

    public VehicleController(
        IVehicleService vehicleService,
        IVehicleCategoryService categoryService,
        IStationService stationService,
        ICurrentUser currentUser)
    {
        _vehicleService = vehicleService;
        _categoryService = categoryService;
        _stationService = stationService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Index(VehicleStatus? status, int? stationId, string? search, int page = 1)
    {
        var filter = new VehicleFilter(status, stationId, search);
        var paged = await _vehicleService.GetPagedAsync(filter, page, 20);
        var stations = await _stationService.ListActiveAsync();
        var vm = new VehicleListViewModel
        {
            Page = paged,
            Status = status,
            StationId = stationId,
            Search = search,
            StationOptions = stations.Select(s => new SelectListItem(s.Name, s.Id.ToString(), s.Id == stationId)),
            StatusOptions = BuildStatusOptions(status)
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var vehicle = await _vehicleService.GetByIdAsync(id);
        if (vehicle is null) return NotFound();
        return View(vehicle);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new VehicleEditViewModel
        {
            Status = VehicleStatus.Available
        };
        await PopulateOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VehicleEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(vm);
            return View(vm);
        }
        var dto = new VehicleUpsertDto(vm.LicensePlate, vm.VehicleCategoryId, vm.HomeStationId, vm.Status);
        var result = await _vehicleService.CreateAsync(dto, _currentUser.Id ?? 0);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not create vehicle.");
            await PopulateOptionsAsync(vm);
            return View(vm);
        }
        TempData["success"] = $"Vehicle {result.Value!.LicensePlate} saved successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var vehicle = await _vehicleService.GetByIdAsync(id);
        if (vehicle is null) return NotFound();
        var vm = new VehicleEditViewModel
        {
            Id = vehicle.Id,
            LicensePlate = vehicle.LicensePlate,
            VehicleCategoryId = vehicle.VehicleCategoryId,
            HomeStationId = vehicle.HomeStationId,
            Status = vehicle.Status,
            RowVersion = vehicle.RowVersion
        };
        await PopulateOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VehicleEditViewModel vm)
    {
        if (id != vm.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(vm);
            return View(vm);
        }
        var dto = new VehicleUpsertDto(vm.LicensePlate, vm.VehicleCategoryId, vm.HomeStationId, vm.Status);
        var result = await _vehicleService.UpdateAsync(id, dto, vm.RowVersion ?? Array.Empty<byte>(), _currentUser.Id ?? 0);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not update vehicle.");
            await PopulateOptionsAsync(vm);
            return View(vm);
        }
        TempData["success"] = $"Vehicle {result.Value!.LicensePlate} updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Decommission(int id)
    {
        var vehicle = await _vehicleService.GetByIdAsync(id);
        if (vehicle is null) return NotFound();
        return View(new VehicleDecommissionViewModel
        {
            Id = vehicle.Id,
            LicensePlate = vehicle.LicensePlate
        });
    }

    [HttpPost, ValidateAntiForgeryToken, ActionName("Decommission")]
    public async Task<IActionResult> DecommissionConfirmed(VehicleDecommissionViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var result = await _vehicleService.DecommissionAsync(vm.Id, vm.Reason, _currentUser.Id ?? 0);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Decommission failed.");
            return View(vm);
        }
        TempData["success"] = $"Vehicle {vm.LicensePlate} decommissioned.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateOptionsAsync(VehicleEditViewModel vm)
    {
        var categories = await _categoryService.ListAsync();
        var stations = await _stationService.ListActiveAsync();
        vm.CategoryOptions = categories.Select(c => new SelectListItem($"{c.Name} ({c.RatePerMinVnd:N0} ₫/min)", c.Id.ToString(), c.Id == vm.VehicleCategoryId));
        vm.StationOptions = stations.Select(s => new SelectListItem($"{s.Name} ({s.CurrentCount}/{s.Capacity})", s.Id.ToString(), s.Id == vm.HomeStationId));
        vm.StatusOptions = BuildStatusOptions(vm.Status);
    }

    private static IEnumerable<SelectListItem> BuildStatusOptions(VehicleStatus? selected) =>
        Enum.GetValues<VehicleStatus>()
            .Where(s => s != VehicleStatus.Decommissioned)
            .Select(s => new SelectListItem(s.ToString(), s.ToString(), selected.HasValue && selected.Value == s));
}
