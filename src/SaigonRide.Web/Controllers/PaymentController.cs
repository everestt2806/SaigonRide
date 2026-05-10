using Microsoft.AspNetCore.Mvc;
using SaigonRide.Data.Repositories;
using SaigonRide.Domain.Enums;
using SaigonRide.Domain.ValueObjects;
using SaigonRide.Services.Audit;
using SaigonRide.Services.Payment;
using SaigonRide.Services.Payment.Gateways;
using SaigonRide.Services.Rentals;

namespace SaigonRide.Web.Controllers;

/// <summary>
/// Handles VNPay sandbox callback endpoints (Return URL and IPN).
/// Part of Tier 4 — live external API integration.
/// </summary>
public class PaymentController : Controller
{
    private readonly VNPayGateway _vnpayGateway;
    private readonly IRentalService _rentalService;
    private readonly IRentalRepository _rentalRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly IStationRepository _stationRepo;
    private readonly ITransactionRepository _transactionRepo;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IEnumerable<IPaymentGateway> gateways,
        IRentalService rentalService,
        IRentalRepository rentalRepo,
        IVehicleRepository vehicleRepo,
        IStationRepository stationRepo,
        ITransactionRepository transactionRepo,
        IUnitOfWork uow,
        IAuditLogger audit,
        ILogger<PaymentController> logger)
    {
        _vnpayGateway = gateways.OfType<VNPayGateway>().First();
        _rentalService = rentalService;
        _rentalRepo = rentalRepo;
        _vehicleRepo = vehicleRepo;
        _stationRepo = stationRepo;
        _transactionRepo = transactionRepo;
        _uow = uow;
        _audit = audit;
        _logger = logger;
    }

    /// <summary>
    /// VNPay Return URL — user is redirected here after completing payment on VNPay sandbox.
    /// Displays payment result to the user.
    /// </summary>
    [HttpGet("Payment/VNPayReturn")]
    public async Task<IActionResult> VNPayReturn()
    {
        var query = Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString());

        var callback = _vnpayGateway.VerifyCallback(query);

        await _audit.LogAsync("VNPAY_RETURN_RECEIVED", "Payment", callback.TxnRef, null,
            new { callback.ResponseCode, callback.TransactionNo, callback.BankCode, callback.Amount });

        if (!callback.IsSuccess)
        {
            _logger.LogWarning("VNPay return: payment failed for TxnRef={TxnRef}, ResponseCode={Code}",
                callback.TxnRef, callback.ResponseCode);

            TempData["error"] = callback.ResponseCode switch
            {
                "24" => "Payment was cancelled.",
                "51" => "Insufficient balance.",
                _ => $"Payment failed (code: {callback.ResponseCode}). Please try again."
            };
            return RedirectToAction("Active", "Rental");
        }

        // Payment succeeded — complete the rental and transaction
        // VNPay returns vnp_TxnRef which matches GatewayRef (truncated orderId), not IdempotencyKey
        var txnRef = callback.TxnRef;
        var transaction = await _transactionRepo.GetByGatewayRefAsync(txnRef);
        if (transaction is not null && transaction.Status != TransactionStatus.Completed)
        {
            transaction.Status = TransactionStatus.Completed;
            transaction.GatewayRef = callback.TransactionNo;
            transaction.PaidAt = DateTime.UtcNow;
            _transactionRepo.Update(transaction);

            // Complete the rental lifecycle: update rental, vehicle, and station
            var rental = await _rentalRepo.GetByIdWithRelationsAsync(transaction.RentalId);
            if (rental is not null && rental.Status == RentalStatus.PaymentPending)
            {
                rental.Status = RentalStatus.Completed;
                _rentalRepo.Update(rental);

                if (rental.Vehicle is not null)
                {
                    rental.Vehicle.ChangeStatus(VehicleStatus.Available);
                    if (rental.ReturnStationId.HasValue)
                        rental.Vehicle.HomeStationId = rental.ReturnStationId.Value;
                    _vehicleRepo.Update(rental.Vehicle);
                }

                if (rental.ReturnStationId.HasValue)
                {
                    var returnStation = await _stationRepo.GetByIdAsync(rental.ReturnStationId.Value);
                    if (returnStation is not null)
                    {
                        returnStation.Increment();
                    }
                }

                await _audit.LogAsync("VNPAY_RENTAL_COMPLETED", "Rental", rental.Id.ToString(), rental.UserId,
                    new { transaction.Amount, callback.TransactionNo });
            }

            await _uow.SaveChangesAsync();
        }

        TempData["success"] = $"VNPay payment of {callback.Amount:N0} VND completed successfully! Transaction: {callback.TransactionNo}";
        return RedirectToAction("Receipt", "Rental", new { id = transaction?.RentalId ?? 0 });
    }

    /// <summary>
    /// VNPay IPN (Instant Payment Notification) — server-to-server callback.
    /// VNPay expects a JSON response with RspCode and Message.
    /// </summary>
    [HttpPost("Payment/VNPayIpn")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> VNPayIpn()
    {
        var query = Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString());

        var callback = _vnpayGateway.VerifyCallback(query);

        await _audit.LogAsync("VNPAY_IPN_RECEIVED", "Payment", callback.TxnRef, null,
            new { callback.ResponseCode, callback.TransactionNo, callback.Amount });

        if (!callback.IsSuccess)
        {
            _logger.LogWarning("VNPay IPN: payment failed for TxnRef={TxnRef}, ResponseCode={Code}",
                callback.TxnRef, callback.ResponseCode);

            return Json(new { RspCode = callback.ResponseCode, Message = "Payment failed" });
        }

        // Update transaction status
        // VNPay returns vnp_TxnRef which matches GatewayRef (truncated orderId), not IdempotencyKey
        var txnRef = callback.TxnRef;
        var transaction = await _transactionRepo.GetByGatewayRefAsync(txnRef);
        if (transaction is null)
        {
            _logger.LogWarning("VNPay IPN: transaction not found for TxnRef={TxnRef}", txnRef);
            return Json(new { RspCode = "01", Message = "Order not found" });
        }

        if (transaction.Status == TransactionStatus.Completed)
        {
            // Already processed — idempotent response
            return Json(new { RspCode = "02", Message = "Order already confirmed" });
        }

        if (transaction.Amount != callback.Amount)
        {
            _logger.LogWarning("VNPay IPN: amount mismatch for TxnRef={TxnRef}. Expected={Expected}, Got={Got}",
                txnRef, transaction.Amount, callback.Amount);
            return Json(new { RspCode = "04", Message = "Invalid Amount" });
        }

        transaction.Status = TransactionStatus.Completed;
        transaction.GatewayRef = callback.TransactionNo;
        transaction.PaidAt = DateTime.UtcNow;
        _transactionRepo.Update(transaction);

        // Complete the rental lifecycle: update rental, vehicle, and station
        var rental = await _rentalRepo.GetByIdWithRelationsAsync(transaction.RentalId);
        if (rental is not null && rental.Status == RentalStatus.PaymentPending)
        {
            rental.Status = RentalStatus.Completed;
            _rentalRepo.Update(rental);

            if (rental.Vehicle is not null)
            {
                rental.Vehicle.ChangeStatus(VehicleStatus.Available);
                if (rental.ReturnStationId.HasValue)
                    rental.Vehicle.HomeStationId = rental.ReturnStationId.Value;
                _vehicleRepo.Update(rental.Vehicle);
            }

            if (rental.ReturnStationId.HasValue)
            {
                var returnStation = await _stationRepo.GetByIdAsync(rental.ReturnStationId.Value);
                if (returnStation is not null)
                {
                    returnStation.Increment();
                }
            }

            await _audit.LogAsync("VNPAY_IPN_RENTAL_COMPLETED", "Rental", rental.Id.ToString(), rental.UserId,
                new { transaction.Amount, callback.TransactionNo });
        }

        await _uow.SaveChangesAsync();

        await _audit.LogAsync("VNPAY_IPN_PROCESSED", "Transaction", transaction.Id.ToString(), null,
            new { callback.TransactionNo, callback.Amount });

        return Json(new { RspCode = "00", Message = "Confirm Success" });
    }
}