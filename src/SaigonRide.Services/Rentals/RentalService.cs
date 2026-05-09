using Microsoft.EntityFrameworkCore;
using SaigonRide.Data.Repositories;
using SaigonRide.Domain.Entities;
using SaigonRide.Domain.Enums;
using SaigonRide.Domain.ValueObjects;
using SaigonRide.Services.Audit;
using SaigonRide.Services.Fares;
using SaigonRide.Services.Payment;

namespace SaigonRide.Services.Rentals;

public interface IRentalService
{
    Task<ServiceResult<Rental>> StartRentalAsync(int userId, int vehicleId, CancellationToken ct = default);
    Task<ServiceResult<FareBreakdown>> PreviewFareAsync(int rentalId, int returnStationId, CancellationToken ct = default);
    Task<ServiceResult<EndRentalOutput>> EndRentalAsync(EndRentalInput input, CancellationToken ct = default);
    Task<ServiceResult> CancelRentalAsync(int rentalId, int userId, CancellationToken ct = default);
    Task<List<Rental>> GetHistoryAsync(int userId, CancellationToken ct = default);
    Task<HistoryPagedResult> GetHistoryPagedAsync(int userId, int page, int pageSize, CancellationToken ct = default);
    Task<Rental?> GetActiveAsync(int userId, CancellationToken ct = default);
    Task<Rental?> GetByIdForUserAsync(int rentalId, int userId, CancellationToken ct = default);
}

public record EndRentalInput(int RentalId, int UserId, int ReturnStationId, PaymentMethod PaymentMethod);
public record EndRentalOutput(Rental Rental, FareBreakdown Fare, Transaction Transaction, string? PaymentUrl = null);
public record HistoryPagedResult(List<Rental> Items, int TotalItems, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
}

/// <summary>
/// UC-02 orchestrator. Coordinates <see cref="StartRentalAsync"/>,
/// <see cref="EndRentalAsync"/> (which delegates fare maths to
/// <see cref="IFareCalculator"/> and the payment to <see cref="IPaymentService"/>),
/// and the free-cancel branch (BR-04, FR-10). Every multi-row write happens
/// inside an EF transaction (see UC-02 §4.2 main scenario step 12).
/// </summary>
public class RentalService : IRentalService
{
    public const int FreeCancellationSeconds = 120;

    private readonly IRentalRepository _rentals;
    private readonly IVehicleRepository _vehicles;
    private readonly IStationRepository _stations;
    private readonly ITransactionRepository _transactions;
    private readonly IUnitOfWork _uow;
    private readonly IFareCalculator _fareCalculator;
    private readonly IPaymentService _payment;
    private readonly IAuditLogger _audit;

    public RentalService(
        IRentalRepository rentals,
        IVehicleRepository vehicles,
        IStationRepository stations,
        ITransactionRepository transactions,
        IUnitOfWork uow,
        IFareCalculator fareCalculator,
        IPaymentService payment,
        IAuditLogger audit)
    {
        _rentals = rentals;
        _vehicles = vehicles;
        _stations = stations;
        _transactions = transactions;
        _uow = uow;
        _fareCalculator = fareCalculator;
        _payment = payment;
        _audit = audit;
    }

    public async Task<ServiceResult<Rental>> StartRentalAsync(int userId, int vehicleId, CancellationToken ct = default)
    {
        var existingActive = await _rentals.GetActiveForUserAsync(userId, ct);
        if (existingActive is not null)
            return ServiceResult<Rental>.Fail("ACTIVE_EXISTS", "You already have an active rental.");

        var vehicle = await _vehicles.GetByIdWithRelationsAsync(vehicleId, ct);
        if (vehicle is null) return ServiceResult<Rental>.Fail("NOT_FOUND", "Vehicle not found.");
        if (vehicle.Status != VehicleStatus.Available)
            return ServiceResult<Rental>.Fail("UNAVAILABLE", "Vehicle is not available.");
        if (vehicle.Category is null)
            return ServiceResult<Rental>.Fail("INVALID", "Vehicle is missing a category.");

        var pickupStation = await _stations.GetByIdAsync(vehicle.HomeStationId, ct);
        if (pickupStation is null) return ServiceResult<Rental>.Fail("STATION_NOT_FOUND", "Pickup station not found.");

        await using var tx = await _uow.BeginTransactionAsync(ct);
        var rental = new Rental
        {
            UserId = userId,
            VehicleId = vehicle.Id,
            PickupStationId = pickupStation.Id,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            Status = RentalStatus.Active,
            RatePerMinSnapshot = vehicle.Category.RatePerMinVnd
        };
        await _rentals.AddAsync(rental, ct);

        vehicle.ChangeStatus(VehicleStatus.InTransit);
        _vehicles.Update(vehicle);
        if (pickupStation.CurrentCount > 0) pickupStation.Decrement();

        await _audit.LogAsync("RENTAL_STARTED", "Rental", null, userId,
            new { vehicle.Id, vehicle.LicensePlate, pickupStationId = pickupStation.Id, rental.RatePerMinSnapshot }, ct);

        try
        {
            await _uow.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult<Rental>.Ok(rental);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            return ServiceResult<Rental>.Fail("CONCURRENCY", "Vehicle was taken by another user.");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            await tx.RollbackAsync(ct);
            return ServiceResult<Rental>.Fail("ACTIVE_EXISTS", "You already have an active rental.");
        }
    }

    public async Task<ServiceResult<FareBreakdown>> PreviewFareAsync(int rentalId, int returnStationId, CancellationToken ct = default)
    {
        var rental = await _rentals.GetByIdWithRelationsAsync(rentalId, ct);
        if (rental is null) return ServiceResult<FareBreakdown>.Fail("NOT_FOUND", "Rental not found.");
        if (rental.Status != RentalStatus.Active)
            return ServiceResult<FareBreakdown>.Fail("INVALID_STATE", "Rental is not active.");

        var returnStation = await _stations.GetByIdAsync(returnStationId, ct);
        if (returnStation is null) return ServiceResult<FareBreakdown>.Fail("STATION_NOT_FOUND", "Return station not found.");

        try
        {
            var fare = _fareCalculator.Calculate(rental.StartTime, DateTime.UtcNow, rental.RatePerMinSnapshot, returnStation);
            return ServiceResult<FareBreakdown>.Ok(fare);
        }
        catch (ArgumentException ex)
        {
            return ServiceResult<FareBreakdown>.Fail("FARE_INVALID", ex.Message);
        }
    }

    public async Task<ServiceResult<EndRentalOutput>> EndRentalAsync(EndRentalInput input, CancellationToken ct = default)
    {
        var rental = await _rentals.GetByIdWithRelationsAsync(input.RentalId, ct);
        if (rental is null) return ServiceResult<EndRentalOutput>.Fail("NOT_FOUND", "Rental not found.");
        if (rental.UserId != input.UserId)
            return ServiceResult<EndRentalOutput>.Fail("FORBIDDEN", "This rental does not belong to you.");
        if (rental.Status != RentalStatus.Active)
            return ServiceResult<EndRentalOutput>.Fail("INVALID_STATE", "Rental is no longer active.");

        var returnStation = await _stations.GetByIdAsync(input.ReturnStationId, ct);
        if (returnStation is null) return ServiceResult<EndRentalOutput>.Fail("STATION_NOT_FOUND", "Return station not found.");

        var endTime = DateTime.UtcNow;
        var fare = _fareCalculator.Calculate(rental.StartTime, endTime, rental.RatePerMinSnapshot, returnStation);

        var idempotencyKey = $"RENT-{rental.Id}-{rental.RowVersion?.GetHashCode():X}";
        await _audit.LogAsync("PAYMENT_INITIATED", "Rental", rental.Id.ToString(), input.UserId,
            new { input.PaymentMethod, fare.TotalFare, idempotencyKey }, ct);

        // M-5: Payment gateway timeout (30 s) to prevent hanging when downstream is unresponsive.
        using var paymentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        paymentCts.CancelAfter(TimeSpan.FromSeconds(30));

        PaymentResult paymentResult;
        try
        {
            paymentResult = await _payment.ProcessAsync(input.PaymentMethod, fare.TotalFare, idempotencyKey, paymentCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await _audit.LogAsync("PAYMENT_TIMEOUT", "Rental", rental.Id.ToString(), input.UserId,
                new { input.PaymentMethod, fare.TotalFare }, ct);
            rental.Status = RentalStatus.PaymentPending;
            _rentals.Update(rental);
            await _uow.SaveChangesAsync(ct);
            return ServiceResult<EndRentalOutput>.Fail("PAYMENT_TIMEOUT",
                "Payment gateway did not respond in time. The rental is held; please retry payment.");
        }

        if (!paymentResult.Success)
        {
            rental.Status = RentalStatus.PaymentPending;
            _rentals.Update(rental);
            await _audit.LogAsync("PAYMENT_FAILED", "Rental", rental.Id.ToString(), input.UserId,
                new { input.PaymentMethod, paymentResult.FailureCode, paymentResult.FailureMessage }, ct);
            await _uow.SaveChangesAsync(ct);
            return ServiceResult<EndRentalOutput>.Fail(paymentResult.FailureCode ?? "PAYMENT_FAILED",
                paymentResult.FailureMessage ?? "Payment failed.");
        }

        // If the gateway returned a PaymentUrl (VNPay), create a pending transaction
        // and return the URL so the controller can redirect the user to the payment page.
        // The rental will be completed after the VNPay callback confirms payment.
        if (!string.IsNullOrEmpty(paymentResult.PaymentUrl))
        {
            await using var tx = await _uow.BeginTransactionAsync(ct);
            rental.EndTime = endTime;
            rental.ReturnStationId = returnStation.Id;
            rental.DurationMinutes = fare.DurationMinutes;
            rental.BaseFare = fare.BaseFare;
            rental.Discount = fare.Discount;
            rental.TotalFare = fare.TotalFare;
            rental.Status = RentalStatus.PaymentPending;
            _rentals.Update(rental);

            var pendingTransaction = new Transaction
            {
                RentalId = rental.Id,
                PaymentMethod = input.PaymentMethod,
                Amount = fare.TotalFare,
                Discount = fare.Discount,
                Status = TransactionStatus.Processing,
                GatewayRef = paymentResult.GatewayRef,
                IdempotencyKey = idempotencyKey,
                PaidAt = DateTime.UtcNow
            };
            await _transactions.AddAsync(pendingTransaction, ct);

            await _audit.LogAsync("VNPAY_REDIRECT_PENDING", "Rental", rental.Id.ToString(), input.UserId,
                new { input.PaymentMethod, fare.TotalFare, paymentResult.GatewayRef }, ct);

            try
            {
                await _uow.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return ServiceResult<EndRentalOutput>.Ok(
                    new EndRentalOutput(rental, fare, pendingTransaction, paymentResult.PaymentUrl));
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<EndRentalOutput>.Fail("CONCURRENCY", "Concurrent update detected. Please retry.");
            }
        }

        // Cash / immediate payment — complete everything now
        await using var tx2 = await _uow.BeginTransactionAsync(ct);
        rental.EndTime = endTime;
        rental.ReturnStationId = returnStation.Id;
        rental.DurationMinutes = fare.DurationMinutes;
        rental.BaseFare = fare.BaseFare;
        rental.Discount = fare.Discount;
        rental.TotalFare = fare.TotalFare;
        rental.Status = RentalStatus.Completed;
        _rentals.Update(rental);

        if (rental.Vehicle is not null)
        {
            rental.Vehicle.ChangeStatus(VehicleStatus.Available);
            rental.Vehicle.HomeStationId = returnStation.Id;
            _vehicles.Update(rental.Vehicle);
        }

        // UC-02 E-3: Always allow return, even when station is at capacity.
        var wasOverflowed = returnStation.IsOverflowed;
        returnStation.Increment();
        if (!wasOverflowed && returnStation.IsOverflowed)
        {
            await _audit.LogAsync("STATION_OVERFLOW", "Station", returnStation.Id.ToString(), input.UserId,
                new { returnStation.Name, returnStation.Capacity, returnStation.CurrentCount }, ct);
        }

        var transaction = new Transaction
        {
            RentalId = rental.Id,
            PaymentMethod = input.PaymentMethod,
            Amount = fare.TotalFare,
            Discount = fare.Discount,
            Status = TransactionStatus.Completed,
            GatewayRef = paymentResult.GatewayRef,
            IdempotencyKey = idempotencyKey,
            PaidAt = endTime
        };
        await _transactions.AddAsync(transaction, ct);

        await _audit.LogAsync("RENTAL_COMPLETED", "Rental", rental.Id.ToString(), input.UserId,
            new { fare.DurationMinutes, fare.BaseFare, fare.Discount, fare.TotalFare, fare.DiscountApplied }, ct);
        await _audit.LogAsync("PAYMENT_SUCCESS", "Transaction", null, input.UserId,
            new { input.PaymentMethod, transaction.Amount, paymentResult.GatewayRef }, ct);

        try
        {
            await _uow.SaveChangesAsync(ct);
            await tx2.CommitAsync(ct);
            return ServiceResult<EndRentalOutput>.Ok(new EndRentalOutput(rental, fare, transaction));
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx2.RollbackAsync(ct);
            return ServiceResult<EndRentalOutput>.Fail("CONCURRENCY", "Concurrent update detected. Please retry.");
        }
    }

    public async Task<ServiceResult> CancelRentalAsync(int rentalId, int userId, CancellationToken ct = default)
    {
        var rental = await _rentals.GetByIdWithRelationsAsync(rentalId, ct);
        if (rental is null) return ServiceResult.Fail("NOT_FOUND", "Rental not found.");
        if (rental.UserId != userId) return ServiceResult.Fail("FORBIDDEN", "This rental does not belong to you.");
        if (rental.Status != RentalStatus.Active) return ServiceResult.Fail("INVALID_STATE", "Rental is not active.");

        var elapsed = DateTime.UtcNow - rental.StartTime;
        if (elapsed.TotalSeconds > FreeCancellationSeconds)
            return ServiceResult.Fail("WINDOW_EXPIRED", "Free-cancellation window has expired.");

        await using var tx = await _uow.BeginTransactionAsync(ct);
        rental.Status = RentalStatus.CancelledFree;
        rental.EndTime = DateTime.UtcNow;
        rental.DurationMinutes = 0;
        rental.BaseFare = 0m;
        rental.Discount = 0m;
        rental.TotalFare = 0m;
        _rentals.Update(rental);

        if (rental.Vehicle is not null)
        {
            rental.Vehicle.ChangeStatus(VehicleStatus.Available);
            _vehicles.Update(rental.Vehicle);
        }

        var pickupStation = await _stations.GetByIdAsync(rental.PickupStationId, ct);
        if (pickupStation is not null)
        {
            pickupStation.Increment();
        }

        await _audit.LogAsync("RENTAL_CANCELLED_FREE", "Rental", rental.Id.ToString(), userId,
            new { ElapsedSeconds = (int)elapsed.TotalSeconds }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public Task<List<Rental>> GetHistoryAsync(int userId, CancellationToken ct = default) =>
        _rentals.ListByUserAsync(userId, ct);

    public async Task<HistoryPagedResult> GetHistoryPagedAsync(int userId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _rentals.Query(tracking: false)
            .Include(r => r.Vehicle!).ThenInclude(v => v.Category!)
            .Include(r => r.PickupStation!)
            .Include(r => r.ReturnStation!)
            .Include(r => r.Transaction!)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.StartTime);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new HistoryPagedResult(items, total, page, pageSize);
    }

    public Task<Rental?> GetActiveAsync(int userId, CancellationToken ct = default) =>
        _rentals.GetActiveForUserAsync(userId, ct);

    public Task<Rental?> GetByIdForUserAsync(int rentalId, int userId, CancellationToken ct = default) =>
        _rentals.GetByIdForUserAsync(rentalId, userId, ct);

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        const string constraintName = "IX_Rentals_OneActivePerUser";
        var msg = ex.InnerException?.Message ?? string.Empty;
        return msg.Contains(constraintName, StringComparison.OrdinalIgnoreCase);
    }
}
