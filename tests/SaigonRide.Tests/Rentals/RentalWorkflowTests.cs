using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SaigonRide.Data;
using SaigonRide.Data.Repositories;
using SaigonRide.Domain.Entities;
using SaigonRide.Domain.Enums;
using SaigonRide.Services.Audit;
using SaigonRide.Services.Fares;
using SaigonRide.Services.Payment;
using SaigonRide.Services.Payment.Gateways;
using SaigonRide.Services.Rentals;
using SaigonRide.Tests.Helpers;
using Xunit;

namespace SaigonRide.Tests.Rentals;

/// <summary>
/// TC-RENTAL: End-to-end rental workflow automation tests.
/// Covers UC-02 main scenario: Start → End → Payment, plus cancellation (BR-04).
/// These are integration tests using InMemory EF Core to verify the full
/// rental lifecycle including vehicle status transitions, station counts,
/// fare calculation, and payment processing.
/// </summary>
public class RentalWorkflowTests : IDisposable
{
    private readonly SaigonRideDbContext _db;
    private readonly RentalService _sut;

    public RentalWorkflowTests()
    {
        _db = InMemoryDbHelper.Create();
        var auditRepo = new AuditLogRepository(_db);
        var audit = new AuditLogger(auditRepo);
        var uow = new UnitOfWork(_db);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VNPay:TmnCode"] = "FAKE_TMN",
                ["VNPay:HashSecret"] = "FAKE_SECRET",
                ["VNPay:BaseUrl"] = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
                ["VNPay:ReturnUrl"] = "https://localhost:5001/Payment/VNPayReturn"
            })
            .Build();

        var paymentService = new PaymentService(new IPaymentGateway[]
        {
            new MoMoGateway(),
            new VNPayGateway(config, new Mock<ILogger<VNPayGateway>>().Object),
            new PayPalGateway(),
            new ApplePayGateway(),
            new CashGateway()
        });

        _sut = new RentalService(
            new RentalRepository(_db),
            new VehicleRepository(_db),
            new StationRepository(_db),
            new TransactionRepository(_db),
            uow,
            new FareCalculator(),
            paymentService,
            audit);

        SeedAsync().GetAwaiter().GetResult();
    }

    private async Task SeedAsync()
    {
        _db.VehicleCategories.Add(
            new VehicleCategory { Id = 1, Name = "Standard Bike", RatePerMinVnd = 500m });
        _db.Users.Add(
            new User
            {
                Id = 1, Email = "test@test.com", PasswordHash = "hash",
                FullName = "Test User", UserType = UserType.LocalCommuter,
                IsActive = true
            });
        _db.Stations.AddRange(
            new Station { Id = 1, Name = "Bến Thành", Address = "District 1", Capacity = 10, CurrentCount = 5, IsActive = true },
            new Station { Id = 2, Name = "Landmark 81", Address = "Bình Thạnh", Capacity = 10, CurrentCount = 3, IsActive = true });
        _db.Vehicles.Add(
            new Vehicle
            {
                Id = 1, LicensePlate = "51A-001", VehicleCategoryId = 1, HomeStationId = 1,
                Status = VehicleStatus.Available
            });
        await _db.SaveChangesAsync();
    }

    // ── TC-RENTAL-01: Start rental succeeds ──
    [Fact]
    public async Task StartRental_succeeds_when_vehicle_available_and_no_active_rental()
    {
        var result = await _sut.StartRentalAsync(userId: 1, vehicleId: 1);

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal(RentalStatus.Active, result.Value.Status);
        Assert.Equal(1, result.Value.UserId);
        Assert.Equal(1, result.Value.VehicleId);

        // Vehicle should be marked as InTransit
        var vehicle = await _db.Vehicles.FindAsync(1);
        Assert.Equal(VehicleStatus.InTransit, vehicle!.Status);

        // Station count should decrease
        var station = await _db.Stations.FindAsync(1);
        Assert.Equal(4, station!.CurrentCount);
    }

    // ── TC-RENTAL-02: Start rental fails when user already has active rental ──
    [Fact]
    public async Task StartRental_fails_when_user_already_has_active_rental()
    {
        await _sut.StartRentalAsync(userId: 1, vehicleId: 1);

        // Add another vehicle and try to start another rental
        _db.Vehicles.Add(new Vehicle
        {
            Id = 2, LicensePlate = "51A-002", VehicleCategoryId = 1, HomeStationId = 1,
            Status = VehicleStatus.Available
        });
        await _db.SaveChangesAsync();

        var result = await _sut.StartRentalAsync(userId: 1, vehicleId: 2);

        Assert.False(result.Success);
        Assert.Equal("ACTIVE_EXISTS", result.ErrorCode);
    }

    // ── TC-RENTAL-03: Start rental fails when vehicle is not available ──
    [Fact]
    public async Task StartRental_fails_when_vehicle_not_available()
    {
        var vehicle = await _db.Vehicles.FindAsync(1);
        vehicle!.Status = VehicleStatus.Maintenance;
        await _db.SaveChangesAsync();

        var result = await _sut.StartRentalAsync(userId: 1, vehicleId: 1);

        Assert.False(result.Success);
        Assert.Equal("UNAVAILABLE", result.ErrorCode);
    }

    // ── TC-RENTAL-04: Start rental fails when vehicle not found ──
    [Fact]
    public async Task StartRental_fails_when_vehicle_not_found()
    {
        var result = await _sut.StartRentalAsync(userId: 1, vehicleId: 999);

        Assert.False(result.Success);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    // ── TC-RENTAL-05: End rental with Cash payment (full workflow) ──
    [Fact]
    public async Task EndRental_with_cash_payment_completes_full_workflow()
    {
        // Start rental
        var startResult = await _sut.StartRentalAsync(userId: 1, vehicleId: 1);
        Assert.True(startResult.Success);
        var rentalId = startResult.Value!.Id;

        // Simulate time passing by adjusting rental start time
        var rental = await _db.Rentals.FindAsync(rentalId);
        rental!.StartTime = DateTime.UtcNow.AddMinutes(-30);
        await _db.SaveChangesAsync();

        // End rental at station 2 with Cash
        var endInput = new EndRentalInput(rentalId, 1, 2, PaymentMethod.Cash);
        var endResult = await _sut.EndRentalAsync(endInput);

        Assert.True(endResult.Success);
        Assert.NotNull(endResult.Value);

        // Verify rental is completed
        Assert.Equal(RentalStatus.Completed, endResult.Value.Rental.Status);
        Assert.NotNull(endResult.Value.Rental.EndTime);

        // Verify fare was calculated
        Assert.True(endResult.Value.Fare.TotalFare > 0);

        // Verify transaction was created
        Assert.NotNull(endResult.Value.Transaction);
        Assert.Equal(TransactionStatus.Completed, endResult.Value.Transaction.Status);

        // Verify vehicle returned to station 2
        var vehicle = await _db.Vehicles.FindAsync(1);
        Assert.Equal(VehicleStatus.Available, vehicle!.Status);
        Assert.Equal(2, vehicle.HomeStationId);

        // Verify station counts updated
        var station1 = await _db.Stations.FindAsync(1);
        var station2 = await _db.Stations.FindAsync(2);
        Assert.Equal(4, station1!.CurrentCount); // Decreased by 1
        Assert.Equal(4, station2!.CurrentCount); // Increased by 1
    }

    // ── TC-RENTAL-06: End rental fails when rental not found ──
    [Fact]
    public async Task EndRental_fails_when_rental_not_found()
    {
        var endInput = new EndRentalInput(999, 1, 1, PaymentMethod.Cash);
        var result = await _sut.EndRentalAsync(endInput);

        Assert.False(result.Success);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    // ── TC-RENTAL-07: Free cancellation within 2 minutes (BR-04) ──
    [Fact]
    public async Task CancelRental_succeeds_within_free_cancellation_window()
    {
        var startResult = await _sut.StartRentalAsync(userId: 1, vehicleId: 1);
        Assert.True(startResult.Success);
        var rentalId = startResult.Value!.Id;

        // Cancel immediately (within 2 minutes)
        var cancelResult = await _sut.CancelRentalAsync(rentalId, 1);

        Assert.True(cancelResult.Success);

        // Verify rental is cancelled (free cancellation)
        var rental = await _db.Rentals.FindAsync(rentalId);
        Assert.Equal(RentalStatus.CancelledFree, rental!.Status);

        // Verify vehicle is available again
        var vehicle = await _db.Vehicles.FindAsync(1);
        Assert.Equal(VehicleStatus.Available, vehicle!.Status);

        // Verify station count restored
        var station = await _db.Stations.FindAsync(1);
        Assert.Equal(5, station!.CurrentCount);
    }

    // ── TC-RENTAL-08: Cancel rental fails for wrong user ──
    [Fact]
    public async Task CancelRental_fails_when_wrong_user()
    {
        var startResult = await _sut.StartRentalAsync(userId: 1, vehicleId: 1);
        Assert.True(startResult.Success);

        var cancelResult = await _sut.CancelRentalAsync(startResult.Value!.Id, userId: 999);

        Assert.False(cancelResult.Success);
        Assert.Equal("FORBIDDEN", cancelResult.ErrorCode);
    }

    // ── TC-RENTAL-09: Get active rental returns current rental ──
    [Fact]
    public async Task GetActive_returns_current_active_rental()
    {
        await _sut.StartRentalAsync(userId: 1, vehicleId: 1);

        var active = await _sut.GetActiveAsync(userId: 1);

        Assert.NotNull(active);
        Assert.Equal(RentalStatus.Active, active.Status);
    }

    // ── TC-RENTAL-10: Get active rental returns null when none active ──
    [Fact]
    public async Task GetActive_returns_null_when_no_active_rental()
    {
        var active = await _sut.GetActiveAsync(userId: 1);

        Assert.Null(active);
    }

    // ── TC-RENTAL-11: Rental history returns completed rentals ──
    [Fact]
    public async Task GetHistory_returns_completed_rentals()
    {
        // Start and complete a rental
        var startResult = await _sut.StartRentalAsync(userId: 1, vehicleId: 1);
        var rental = await _db.Rentals.FindAsync(startResult.Value!.Id);
        rental!.StartTime = DateTime.UtcNow.AddMinutes(-15);
        await _db.SaveChangesAsync();

        await _sut.EndRentalAsync(new EndRentalInput(rental.Id, 1, 2, PaymentMethod.Cash));

        var history = await _sut.GetHistoryAsync(userId: 1);

        Assert.Single(history);
        Assert.Equal(RentalStatus.Completed, history[0].Status);
    }

    // ── TC-RENTAL-12: Preview fare returns valid breakdown ──
    [Fact]
    public async Task PreviewFare_returns_valid_fare_breakdown()
    {
        var startResult = await _sut.StartRentalAsync(userId: 1, vehicleId: 1);
        var rental = await _db.Rentals.FindAsync(startResult.Value!.Id);
        rental!.StartTime = DateTime.UtcNow.AddMinutes(-20);
        await _db.SaveChangesAsync();

        var preview = await _sut.PreviewFareAsync(rental.Id, returnStationId: 2);

        Assert.True(preview.Success);
        Assert.NotNull(preview.Value);
        Assert.True(preview.Value.TotalFare > 0);
    }

    public void Dispose() => _db.Dispose();
}