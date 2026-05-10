using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonRide.Data.Repositories;
using SaigonRide.Domain.Enums;
using SaigonRide.Services.Inventory;

namespace SaigonRide.Web.Controllers;

/// <summary>
/// Browse stations + available vehicles per station. Authenticated end users
/// reach the rental flow from <see cref="Details"/>. Admin maintenance uses
/// the dedicated Admin/Stations module (not in scope for Phase 3 MVP).
/// </summary>
[Authorize]
public class StationController : Controller
{
    private readonly IStationService _stationService;
    private readonly IVehicleRepository _vehicleRepository;

    public StationController(IStationService stationService, IVehicleRepository vehicleRepository)
    {
        _stationService = stationService;
        _vehicleRepository = vehicleRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var stations = await _stationService.ListWithVehicleCountsAsync();
        return View(stations);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var station = await _stationService.GetByIdAsync(id);
        if (station is null) return NotFound();

        var vehicles = await _vehicleRepository.Query(tracking: false)
            .Include(v => v.Category!)
            .Where(v => v.HomeStationId == id && v.Status == VehicleStatus.Available)
            .OrderBy(v => v.Category!.Name)
            .ToListAsync();

        ViewBag.Vehicles = vehicles;
        return View(station);
    }
}
