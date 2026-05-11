using SaigonRide.Data;
using SaigonRide.Data.Repositories;
using SaigonRide.Domain.Entities;
using SaigonRide.Domain.Enums;
using SaigonRide.Services.Dashboard;
using SaigonRide.Tests.Helpers;
using Xunit;

namespace SaigonRide.Tests.Dashboard;

/// <summary>
/// TC-DASH: Admin Dashboard service unit tests.
/// Verifies that the dashboard correctly aggregates system metrics
/// including fleet status, rental counts, revenue, and station utilisation.
/// </summary>
public class DashboardServiceTests : IDisposable
{
    private readonly SaigonRideDbContext _db;
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _db = InMemoryDbHelper.Create();

        var users = new UserRepository(_db);
        var vehicles = new VehicleRepository(_db);
        var stations = new StationRepository(_db);
        var rentals = new RentalRepository(_db);
        var transactions = new TransactionRepository(_db);
        var auditLogs = new AuditLogRepository(_db);

        _sut = new DashboardService(users, vehicles, stations, rentals, transactions, auditLogs);

        SeedAsync().GetAwaiter().GetResult();
    }

    private async Task SeedAsync()
    {
        _db.VehicleCategories.Add(
            new VehicleCategory { Id = 1, Name = "Standard Bike", RatePerMinVnd = 500m });

        _db.Users.AddRange(
            new User { Id = 1, Email = "admin@test.com", PasswordHash = "hash", FullName = "Admin", UserType = UserType.Admin, IsActive = true },
            new User { Id = 2, Email = "user1@test.com", PasswordHash = "hash", FullName = "User 1", UserType = UserType.LocalCommuter, IsActive = true },
            new User { Id = 3, Email = "user2@test.com", PasswordHash = "hash", FullName = "User 2", UserType = UserType.LocalCommuter, IsActive = true },
            new User { Id = 4, Email = "deleted@test.com", PasswordHash = "hash", FullName = "Deleted", UserType = UserType.LocalCommuter, IsActive = true, IsDeleted = true });

        _db.Stations.AddRange(
            new Station { Id = 1, Name = "Bến Thành", Address = "District 1", Capacity = 10, CurrentCount = 3, IsActive = true },
            new Station { Id = 2, Name = "Landmark 81", Address = "Bình Thạnh", Capacity = 20, CurrentCount = 8, IsActive = true },
            new Station { Id = 3, Name = "Inactive Station", Address = "Nowhere", Capacity = 5, CurrentCount = 0, IsActive = false });

        _db.Vehicles.AddRange(
            new Vehicle { Id = 1, LicensePlate = "51A-001", VehicleCategoryId = 1, HomeStationId = 1, Status = VehicleStatus.Available },
            new Vehicle { Id = 2, LicensePlate = "51A-002", VehicleCategoryId = 1, HomeStationId = 1, Status = VehicleStatus.Available },
            new Vehicle { Id = 3, LicensePlate = "51A-003", VehicleCategoryId = 1, HomeStationId = 1, Status = VehicleStatus.InTransit },
            new Vehicle { Id = 4, LicensePlate = "51A-004", VehicleCategoryId = 1, HomeStationId = 2, Status = VehicleStatus.Maintenance });

        _db.Rentals.AddRange(
            new Rental { Id = 1, UserId = 2, VehicleId = 1, PickupStationId = 1, StartTime = DateTime.UtcNow.AddHours(-2), EndTime = DateTime.UtcNow.AddHours(-1), Status = RentalStatus.Completed, RatePerMinSnapshot = 500m, TotalFare = 30000m },
            new Rental { Id = 2, UserId = 3, VehicleId = 2, PickupStationId = 1, StartTime = DateTime.UtcNow.AddHours(-1), Status = RentalStatus.Active, RatePerMinSnapshot = 500m },
            new Rental { Id = 3, UserId = 2, VehicleId = 3, PickupStationId = 2, StartTime = DateTime.UtcNow.AddDays(-1), EndTime = DateTime.UtcNow.AddDays(-1).AddHours(1), Status = RentalStatus.Completed, RatePerMinSnapshot = 500m, TotalFare = 30000m },
            new Rental { Id = 4, UserId = 3, VehicleId = 1, PickupStationId = 1, StartTime = DateTime.UtcNow.AddDays(-2), Status = RentalStatus.CancelledFree, RatePerMinSnapshot = 500m });

        _db.Transactions.AddRange(
            new Transaction { Id = 1, RentalId = 1, Amount = 30000m, PaymentMethod = PaymentMethod.Cash, Status = TransactionStatus.Completed, GatewayRef = "CASH-1", PaidAt = DateTime.UtcNow.AddHours(-1) },
            new Transaction { Id = 2, RentalId = 3, Amount = 30000m, PaymentMethod = PaymentMethod.VNPay, Status = TransactionStatus.Completed, GatewayRef = "VNPAY-1", PaidAt = DateTime.UtcNow.AddDays(-1) });

        await _db.SaveChangesAsync();
    }

    // ── TC-DASH-01: Summary returns correct aggregated metrics ──
    [Fact]
    public async Task GetSummary_returns_correct_aggregated_metrics()
    {
        var result = await _sut.GetSummaryAsync();

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalUsers); // Excludes soft-deleted users
        Assert.Equal(4, result.TotalVehicles);
        Assert.Equal(2, result.TotalStations); // Only active stations
        Assert.Equal(1, result.ActiveRentals);
        Assert.Equal(2, result.TotalTransactions);
        Assert.Equal(60000m, result.TotalRevenue);
        Assert.Equal(2, result.VehiclesAvailable);
        Assert.Equal(1, result.VehiclesRented);
    }

    // ── TC-DASH-02: Daily revenue returns grouped data ──
    [Fact]
    public async Task GetDailyRevenue_returns_grouped_revenue_data()
    {
        var result = await _sut.GetDailyRevenueAsync(30);

        Assert.NotNull(result);
        Assert.True(result.Count > 0);
        Assert.True(result.All(p => p.Revenue > 0));
        Assert.True(result.All(p => p.TripCount > 0));
    }

    // ── TC-DASH-03: Payment method breakdown returns correct groups ──
    [Fact]
    public async Task GetPaymentMethodBreakdown_returns_correct_groups()
    {
        var result = await _sut.GetPaymentMethodBreakdownAsync();

        Assert.NotNull(result);
        Assert.Equal(2, result.Count); // Cash and VNPay

        var cash = result.First(p => p.Method == "Cash");
        Assert.Equal(1, cash.Count);
        Assert.Equal(30000m, cash.Total);

        var vnpay = result.First(p => p.Method == "VNPay");
        Assert.Equal(1, vnpay.Count);
        Assert.Equal(30000m, vnpay.Total);
    }

    // ── TC-DASH-04: Recent activity returns limited results ──
    [Fact]
    public async Task GetRecentActivity_returns_limited_results()
    {
        var result = await _sut.GetRecentActivityAsync(5);

        Assert.NotNull(result);
        Assert.True(result.Count <= 5);
    }

    // ── TC-DASH-05: Summary with empty database returns zeros ──
    [Fact]
    public async Task GetSummary_with_empty_database_returns_zeros()
    {
        var freshDb = InMemoryDbHelper.Create("empty-dash");
        var freshService = new DashboardService(
            new UserRepository(freshDb),
            new VehicleRepository(freshDb),
            new StationRepository(freshDb),
            new RentalRepository(freshDb),
            new TransactionRepository(freshDb),
            new AuditLogRepository(freshDb));

        var result = await freshService.GetSummaryAsync();

        Assert.NotNull(result);
        Assert.Equal(0, result.TotalUsers);
        Assert.Equal(0, result.TotalVehicles);
        Assert.Equal(0, result.TotalStations);
        Assert.Equal(0, result.ActiveRentals);
        Assert.Equal(0, result.TotalRevenue);

        freshDb.Dispose();
    }

    // ── TC-DASH-06: Daily revenue with no data returns empty ──
    [Fact]
    public async Task GetDailyRevenue_with_no_data_returns_empty()
    {
        var freshDb = InMemoryDbHelper.Create("empty-revenue");
        var freshService = new DashboardService(
            new UserRepository(freshDb),
            new VehicleRepository(freshDb),
            new StationRepository(freshDb),
            new RentalRepository(freshDb),
            new TransactionRepository(freshDb),
            new AuditLogRepository(freshDb));

        var result = await freshService.GetDailyRevenueAsync(30);

        Assert.NotNull(result);
        Assert.Empty(result);

        freshDb.Dispose();
    }

    public void Dispose() => _db.Dispose();
}