using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonRide.Services.Inventory;
using SaigonRide.Web.Infrastructure;
using SaigonRide.Web.Models;

namespace SaigonRide.Web.Controllers;

[Authorize(Roles = "Admin")]
public class VehicleCategoryController : Controller
{
    private readonly IVehicleCategoryService _service;
    private readonly ICurrentUser _currentUser;

    public VehicleCategoryController(IVehicleCategoryService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var list = await _service.ListAsync();
        return View(list);
    }

    [HttpGet]
    public IActionResult Create() => View(new VehicleCategoryViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VehicleCategoryViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var result = await _service.CreateAsync(vm.Name, vm.RatePerMinVnd, _currentUser.Id ?? 0);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not create category.");
            return View(vm);
        }
        TempData["success"] = $"Category '{result.Value!.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await _service.GetByIdAsync(id);
        if (category is null) return NotFound();
        return View(new VehicleCategoryViewModel { Id = category.Id, Name = category.Name, RatePerMinVnd = category.RatePerMinVnd });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VehicleCategoryViewModel vm)
    {
        if (id != vm.Id) return BadRequest();
        if (!ModelState.IsValid) return View(vm);
        var result = await _service.UpdateAsync(id, vm.Name, vm.RatePerMinVnd, _currentUser.Id ?? 0);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not update category.");
            return View(vm);
        }
        TempData["success"] = $"Category '{result.Value!.Name}' updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _service.DeleteAsync(id, _currentUser.Id ?? 0);
        if (!result.Success)
        {
            TempData["error"] = result.ErrorMessage;
        }
        else
        {
            TempData["success"] = "Category deleted.";
        }
        return RedirectToAction(nameof(Index));
    }
}
