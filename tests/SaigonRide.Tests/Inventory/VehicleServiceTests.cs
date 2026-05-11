using Microsoft.EntityFrameworkCore;
using SaigonRide.Data;
using SaigonRide.Data.Repositories;
using SaigonRide.Domain.Entities;
using SaigonRide.Domain.Enums;
using SaigonRide.Services.Audit;
using SaigonRide.Services.Inventory;
using SaigonRide.Tests.Helpers;
using Xunit;

namespace SaigonRide.Tests.Inventory;

/// <summary>
/// TC-VEH: VehicleService unit tests covering UC-01 main scenario,
/// duplicate-plate guard (E-1), full-station guard (E-2), in-transit
/// decommission guard (E-6), category in-use guard (E-7).
/// </summary>
public class VehicleServiceTests : IDisposable
{
    private readonly SaigonRideDbContext _db;
    private readonly VehicleService _sut;
    private readonly VehicleCategoryService _categoryService;

    public VehicleServiceTests()
    {
        _db = InMemoryDbHelper.Create();
        var auditRepo = new AuditLogRepository(_db);
        var audit = new AuditLogger(auditRepo);
        var uow = new UnitOfWork(_db);
        _sut = new VehicleService(
            new VehicleRepository(_db),
            new StationRepository(_db),
            new MaintenanceLogRepository(_db),
            uow,
            audit);
        _categoryService = new VehicleCategoryService(
            new VehicleCategoryRepository(_db),
            new VehicleRepository(_db),
            uow,
            audit);

        SeedAsync().GetAwaiter().GetResult();
    }

    private async Task SeedAsync()
    {
        _db.VehicleCategories.AddRange(
            new VehicleCategory { Id = 1, Name = "Standard Bike", RatePerMinVnd = 500m },
            new VehicleCategory { Id = 2, Name = "E-Scooter", RatePerMinVnd = 1500m });
        _db.Stations.AddRange(
            new Station { Id = 1, Name = "Bến Thành", Address = "x", Capacity = 10, CurrentCount = 4, IsActive = true },
            new Station { Id = 2, Name = "Landmark 81", Address = "y", Capacity = 5,  CurrentCount = 5, IsActive = true });
        await _db.SaveChangesAsync();
    }

    [Fact] // TC-VEH-01
    public async Task Create_succeeds_when_inputs_valid_and_station_has_capacity()
    {
        var dto = new VehicleUpsertDto("51H1-1234", 1, 1, VehicleStatus.Available);

        var result = await _sut.CreateAsync(dto, actingUserId: 1);

        Assert.True(result.Success);
        var station = await _db.Stations.FindAsync(1);
        Assert.Equal(5, station!.CurrentCount);
        Assert.Single(_db.AuditLogs);
        Assert.Equal("VEHICLE_CREATED", _db.AuditLogs.First().Action);
    }

    [Fact] // TC-VEH-02
    public async Task Create_fails_when_plate_duplicates_existing_vehicle()
    {
        await _sut.CreateAsync(new VehicleUpsertDto("51H1-1234", 1, 1, VehicleStatus.Available), 1);

        var second = await _sut.CreateAsync(new VehicleUpsertDto("51H1-1234", 1, 1, VehicleStatus.Available), 1);

        Assert.False(second.Success);
        Assert.Equal("PLATE_DUPLICATE", second.ErrorCode);
    }

    [Fact] // TC-VEH-03
    public async Task Create_fails_when_target_station_is_at_full_capacity()
    {
        var result = await _sut.CreateAsync(new VehicleUpsertDto("51H1-2222", 1, 2, VehicleStatus.Available), 1);

        Assert.False(result.Success);
        Assert.Equal("STATION_FULL", result.ErrorCode);
    }

    [Fact] // TC-VEH-04
    public async Task Decommission_fails_when_vehicle_is_in_transit()
    {
        await _sut.CreateAsync(new VehicleUpsertDto("51H1-3333", 1, 1, VehicleStatus.Available), 1);
        var vehicle = await _db.Vehicles.FirstAsync();
        vehicle.Status = VehicleStatus.InTransit;
        await _db.SaveChangesAsync();

        var result = await _sut.DecommissionAsync(vehicle.Id, "scrap", 1);

        Assert.False(result.Success);
        Assert.Equal("IN_TRANSIT", result.ErrorCode);
    }

    [Fact] // TC-VEH-05
    public async Task Category_delete_fails_when_vehicles_still_reference_it()
    {
        await _sut.CreateAsync(new VehicleUpsertDto("51H1-4444", 1, 1, VehicleStatus.Available), 1);

        var result = await _categoryService.DeleteAsync(1, 1);

        Assert.False(result.Success);
        Assert.Equal("IN_USE", result.ErrorCode);
    }

    public void Dispose() => _db.Dispose();
}
