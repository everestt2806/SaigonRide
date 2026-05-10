using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonRide.Domain.Enums;
using SaigonRide.Services.Inventory;
using SaigonRide.Web.Models;

namespace SaigonRide.Web.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    private readonly IStationService _stationService;
    private readonly IVehicleCategoryService _categoryService;
    private readonly IVehicleService _vehicleService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IStationService stationService,
        IVehicleCategoryService categoryService,
        IVehicleService vehicleService,
        ILogger<HomeController> logger)
    {
        _stationService = stationService;
        _categoryService = categoryService;
        _vehicleService = vehicleService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var stations = await _stationService.ListActiveAsync();
        var categories = await _categoryService.ListAsync();
        var vehicleCount = await _vehicleService.CountAvailableAsync();

        var vm = new HomeViewModel
        {
            ActiveStationCount = stations.Count,
            AvailableVehicleCount = vehicleCount,
            Categories = categories,
            TopStations = stations.OrderByDescending(s => s.CurrentCount).Take(5).ToList()
        };
        return View(vm);
    }

    public IActionResult About() => View();
}
