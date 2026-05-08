using Microsoft.EntityFrameworkCore;
using SaigonRide.Data.Repositories;
using SaigonRide.Domain.Entities;
using SaigonRide.Domain.Enums;
using SaigonRide.Domain.ValueObjects;
using SaigonRide.Services.Audit;

namespace SaigonRide.Services.Inventory;

public interface IVehicleService
{
    Task<PagedResult<Vehicle>> GetPagedAsync(VehicleFilter filter, int page, int pageSize, CancellationToken ct = default);
    Task<Vehicle?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<Vehicle>> CreateAsync(VehicleUpsertDto dto, int actingUserId, CancellationToken ct = default);
    Task<ServiceResult<Vehicle>> UpdateAsync(int id, VehicleUpsertDto dto, byte[] rowVersion, int actingUserId, CancellationToken ct = default);
    Task<ServiceResult> DecommissionAsync(int id, string reason, int actingUserId, CancellationToken ct = default);
    Task<int> CountAvailableAsync(CancellationToken ct = default);
}

public record VehicleFilter(VehicleStatus? Status, int? StationId, string? Search);
public record VehicleUpsertDto(string LicensePlate, int VehicleCategoryId, int HomeStationId, VehicleStatus Status);
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalItems, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalItems / PageSize);
}

/// <summary>
/// UC-01 Manage Vehicle Inventory. Owns CRUD orchestration + the station
/// counter invariants. The mutating methods open an EF transaction so the
/// vehicle row, the station counter, the maintenance log and the audit log
/// commit (or roll back) together (UC-01 main scenario steps 7-10).
/// </summary>
public class VehicleService : IVehicleService
{
    private readonly IVehicleRepository _vehicles;
    private readonly IStationRepository _stations;
    private readonly IMaintenanceLogRepository _maintenance;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public VehicleService(
        IVehicleRepository vehicles,
        IStationRepository stations,
        IMaintenanceLogRepository maintenance,
        IUnitOfWork uow,
        IAuditLogger audit)
    {
        _vehicles = vehicles;
        _stations = stations;
        _maintenance = maintenance;
        _uow = uow;
        _audit = audit;
    }

    public async Task<PagedResult<Vehicle>> GetPagedAsync(VehicleFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _vehicles.Query(tracking: false)
            .Include(v => v.Category!)
            .Include(v => v.HomeStation!)
            .AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(v => v.Status == filter.Status.Value);
        if (filter.StationId.HasValue)
            query = query.Where(v => v.HomeStationId == filter.StationId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Search))
            query = query.Where(v => v.LicensePlate.Contains(filter.Search));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(v => v.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new PagedResult<Vehicle>(items, total, page, pageSize);
    }

    public Task<Vehicle?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _vehicles.GetByIdWithRelationsAsync(id, ct);

    public async Task<ServiceResult<Vehicle>> CreateAsync(VehicleUpsertDto dto, int actingUserId, CancellationToken ct = default)
    {
        var validation = ValidateDto(dto);
        if (validation is not null) return validation;

        if (await _vehicles.ExistsByPlateAsync(dto.LicensePlate, null, ct))
            return ServiceResult<Vehicle>.Fail("PLATE_DUPLICATE", $"License plate '{dto.LicensePlate}' is already registered.");

        var station = await _stations.GetByIdAsync(dto.HomeStationId, ct);
        if (station is null) return ServiceResult<Vehicle>.Fail("STATION_NOT_FOUND", "The selected station does not exist.");
        if (!station.HasCapacity()) return ServiceResult<Vehicle>.Fail("STATION_FULL", $"Station '{station.Name}' is at full capacity.");

        await using var tx = await _uow.BeginTransactionAsync(ct);
        var entity = new Vehicle
        {
            LicensePlate = dto.LicensePlate.Trim().ToUpperInvariant(),
            VehicleCategoryId = dto.VehicleCategoryId,
            HomeStationId = dto.HomeStationId,
            Status = dto.Status
        };
        await _vehicles.AddAsync(entity, ct);

        if (entity.Status != VehicleStatus.Decommissioned)
            station.Increment();

        await _audit.LogAsync("VEHICLE_CREATED", "Vehicle", null, actingUserId,
            new { entity.LicensePlate, entity.VehicleCategoryId, entity.HomeStationId, entity.Status }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ServiceResult<Vehicle>.Ok(entity);
    }

    public async Task<ServiceResult<Vehicle>> UpdateAsync(int id, VehicleUpsertDto dto, byte[] rowVersion, int actingUserId, CancellationToken ct = default)
    {
        var validation = ValidateDto(dto);
        if (validation is not null) return validation;

        var entity = await _vehicles.GetByIdAsync(id, ct);
        if (entity is null) return ServiceResult<Vehicle>.Fail("NOT_FOUND", "Vehicle not found.");
        if (entity.Status == VehicleStatus.Decommissioned)
            return ServiceResult<Vehicle>.Fail("DECOMMISSIONED", "Decommissioned vehicles cannot be edited.");

        if (await _vehicles.ExistsByPlateAsync(dto.LicensePlate, id, ct))
            return ServiceResult<Vehicle>.Fail("PLATE_DUPLICATE", $"License plate '{dto.LicensePlate}' is already in use.");

        var newStation = await _stations.GetByIdAsync(dto.HomeStationId, ct);
        if (newStation is null) return ServiceResult<Vehicle>.Fail("STATION_NOT_FOUND", "The selected station does not exist.");

        await using var tx = await _uow.BeginTransactionAsync(ct);
        entity.RowVersion = rowVersion;

        var previousStatus = entity.Status;
        var previousStationId = entity.HomeStationId;

        if (entity.HomeStationId != dto.HomeStationId)
        {
            var oldStation = await _stations.GetByIdAsync(previousStationId, ct);
            if (oldStation is not null && oldStation.CurrentCount > 0)
                oldStation.Decrement();
            if (!newStation.HasCapacity())
                return ServiceResult<Vehicle>.Fail("STATION_FULL", $"Station '{newStation.Name}' is at full capacity.");
            newStation.Increment();
            entity.HomeStationId = dto.HomeStationId;
        }

        entity.LicensePlate = dto.LicensePlate.Trim().ToUpperInvariant();
        entity.VehicleCategoryId = dto.VehicleCategoryId;

        if (previousStatus != dto.Status)
        {
            entity.ChangeStatus(dto.Status);
            if (previousStatus == VehicleStatus.Maintenance || dto.Status == VehicleStatus.Maintenance)
            {
                await _maintenance.AddAsync(new Domain.Entities.MaintenanceLog
                {
                    VehicleId = entity.Id,
                    UserId = actingUserId,
                    FromStatus = previousStatus,
                    ToStatus = dto.Status,
                    Notes = "Maintenance status transition via VehicleService.UpdateAsync"
                }, ct);
            }
        }

        _vehicles.Update(entity);
        await _audit.LogAsync("VEHICLE_UPDATED", "Vehicle", entity.Id.ToString(), actingUserId,
            new { entity.LicensePlate, previousStatus, NewStatus = entity.Status, previousStationId, NewStationId = entity.HomeStationId }, ct);

        try
        {
            await _uow.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult<Vehicle>.Ok(entity);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            return ServiceResult<Vehicle>.Fail("CONCURRENCY",
                "Another admin has edited this vehicle since you opened the form. Reload to see the latest version.");
        }
    }

    public async Task<ServiceResult> DecommissionAsync(int id, string reason, int actingUserId, CancellationToken ct = default)
    {
        var entity = await _vehicles.GetByIdAsync(id, ct);
        if (entity is null) return ServiceResult.Fail("NOT_FOUND", "Vehicle not found.");
        if (entity.Status == VehicleStatus.InTransit)
            return ServiceResult.Fail("IN_TRANSIT", "Vehicle is on an active rental — wait for return before decommissioning.");
        if (entity.IsDeleted) return ServiceResult.Fail("ALREADY_DECOMMISSIONED", "Vehicle is already decommissioned.");

        await using var tx = await _uow.BeginTransactionAsync(ct);
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.Status = VehicleStatus.Decommissioned;
        entity.DecommissionReason = reason;
        _vehicles.Update(entity);

        var station = await _stations.GetByIdAsync(entity.HomeStationId, ct);
        if (station is not null && station.CurrentCount > 0)
            station.Decrement();

        await _audit.LogAsync("VEHICLE_DECOMMISSIONED", "Vehicle", entity.Id.ToString(), actingUserId,
            new { entity.LicensePlate, reason }, ct);

        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public Task<int> CountAvailableAsync(CancellationToken ct = default) =>
        _vehicles.Query(tracking: false)
            .CountAsync(v => v.Status == VehicleStatus.Available, ct);

    private static ServiceResult<Vehicle>? ValidateDto(VehicleUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.LicensePlate))
            return ServiceResult<Vehicle>.Fail("PLATE_REQUIRED", "License plate is required.");
        if (dto.LicensePlate.Length > 20)
            return ServiceResult<Vehicle>.Fail("PLATE_LENGTH", "License plate must be 20 characters or fewer.");
        if (dto.VehicleCategoryId <= 0)
            return ServiceResult<Vehicle>.Fail("CATEGORY_REQUIRED", "Vehicle category is required.");
        if (dto.HomeStationId <= 0)
            return ServiceResult<Vehicle>.Fail("STATION_REQUIRED", "Home station is required.");
        return null;
    }
}
