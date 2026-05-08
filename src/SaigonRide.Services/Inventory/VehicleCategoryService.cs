using Microsoft.EntityFrameworkCore;
using SaigonRide.Data.Repositories;
using SaigonRide.Domain.Entities;
using SaigonRide.Domain.ValueObjects;
using SaigonRide.Services.Audit;

namespace SaigonRide.Services.Inventory;

public interface IVehicleCategoryService
{
    Task<List<VehicleCategory>> ListAsync(CancellationToken ct = default);
    Task<VehicleCategory?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<VehicleCategory>> CreateAsync(string name, decimal ratePerMinVnd, int actingUserId, CancellationToken ct = default);
    Task<ServiceResult<VehicleCategory>> UpdateAsync(int id, string name, decimal ratePerMinVnd, int actingUserId, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(int id, int actingUserId, CancellationToken ct = default);
}

/// <summary>
/// FR-05 Manage Vehicle Categories. Guarded against deletion when any active
/// vehicle still references the category (UC-01 E-7).
/// </summary>
public class VehicleCategoryService : IVehicleCategoryService
{
    private readonly IVehicleCategoryRepository _categories;
    private readonly IVehicleRepository _vehicles;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public VehicleCategoryService(IVehicleCategoryRepository categories, IVehicleRepository vehicles, IUnitOfWork uow, IAuditLogger audit)
    {
        _categories = categories;
        _vehicles = vehicles;
        _uow = uow;
        _audit = audit;
    }

    public Task<List<VehicleCategory>> ListAsync(CancellationToken ct = default) =>
        _categories.Query(tracking: false).OrderBy(c => c.Name).ToListAsync(ct);

    public Task<VehicleCategory?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _categories.GetByIdAsync(id, ct);

    public async Task<ServiceResult<VehicleCategory>> CreateAsync(string name, decimal ratePerMinVnd, int actingUserId, CancellationToken ct = default)
    {
        var validation = Validate(name, ratePerMinVnd);
        if (validation is not null) return validation;

        if (await _categories.AnyAsync(c => c.Name == name, ct))
            return ServiceResult<VehicleCategory>.Fail("NAME_DUPLICATE", $"A category named '{name}' already exists.");

        var category = new VehicleCategory { Name = name.Trim(), RatePerMinVnd = ratePerMinVnd };
        await _categories.AddAsync(category, ct);
        await _audit.LogAsync("CATEGORY_CREATED", "VehicleCategory", null, actingUserId, new { category.Name, category.RatePerMinVnd }, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<VehicleCategory>.Ok(category);
    }

    public async Task<ServiceResult<VehicleCategory>> UpdateAsync(int id, string name, decimal ratePerMinVnd, int actingUserId, CancellationToken ct = default)
    {
        var validation = Validate(name, ratePerMinVnd);
        if (validation is not null) return validation;

        var category = await _categories.GetByIdAsync(id, ct);
        if (category is null) return ServiceResult<VehicleCategory>.Fail("NOT_FOUND", "Category not found.");

        if (await _categories.AnyAsync(c => c.Name == name && c.Id != id, ct))
            return ServiceResult<VehicleCategory>.Fail("NAME_DUPLICATE", $"A category named '{name}' already exists.");

        var previousRate = category.RatePerMinVnd;
        category.Name = name.Trim();
        category.RatePerMinVnd = ratePerMinVnd;
        _categories.Update(category);
        await _audit.LogAsync("CATEGORY_UPDATED", "VehicleCategory", category.Id.ToString(), actingUserId,
            new { category.Name, previousRate, NewRate = ratePerMinVnd }, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<VehicleCategory>.Ok(category);
    }

    public async Task<ServiceResult> DeleteAsync(int id, int actingUserId, CancellationToken ct = default)
    {
        var category = await _categories.GetByIdAsync(id, ct);
        if (category is null) return ServiceResult.Fail("NOT_FOUND", "Category not found.");

        var activeCount = await _vehicles.Query(tracking: false)
            .Where(v => v.VehicleCategoryId == id)
            .CountAsync(ct);
        if (activeCount > 0)
            return ServiceResult.Fail("IN_USE", $"Category has {activeCount} active vehicle(s). Reassign or decommission them first.");

        category.IsDeleted = true;
        _categories.Update(category);
        await _audit.LogAsync("CATEGORY_DELETED", "VehicleCategory", category.Id.ToString(), actingUserId, new { category.Name }, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static ServiceResult<VehicleCategory>? Validate(string name, decimal rate)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ServiceResult<VehicleCategory>.Fail("NAME_REQUIRED", "Category name is required.");
        if (name.Length > 80)
            return ServiceResult<VehicleCategory>.Fail("NAME_LENGTH", "Category name must be 80 characters or fewer.");
        if (rate <= 0)
            return ServiceResult<VehicleCategory>.Fail("RATE_INVALID", "Rate per minute must be greater than zero.");
        return null;
    }
}
