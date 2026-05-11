using Microsoft.EntityFrameworkCore;
using SaigonRide.Data.Repositories;
using SaigonRide.Domain.Enums;

namespace SaigonRide.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default);
    Task<List<DailyRevenuePoint>> GetDailyRevenueAsync(int days = 30, CancellationToken ct = default);
    Task<List<PaymentMethodBreakdown>> GetPaymentMethodBreakdownAsync(CancellationToken ct = default);
    Task<List<RecentActivityItem>> GetRecentActivityAsync(int count = 10, CancellationToken ct = default);
}

public record DashboardSummary(
    int TotalUsers,
    int TotalVehicles,
    int TotalStations,
    int ActiveRentals,
    int TotalTransactions,
    decimal TotalRevenue,
    decimal TodayRevenue,
    int TodayTransactions,
    decimal StationOccupancyAvg,
    int VehiclesAvailable,
    int VehiclesRented
);

public record DailyRevenuePoint(string Date, decimal Revenue, int TripCount);
public record PaymentMethodBreakdown(string Method, int Count, decimal Total);
public record RecentActivityItem(string Action, string EntityType, string? EntityId, DateTime Timestamp, string? UserEmail);

/// <summary>
/// Aggregates system-wide metrics for the Admin Dashboard.
/// Provides real-time monitoring data from multiple domain tables.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly IUserRepository _users;
    private readonly IVehicleRepository _vehicles;
    private readonly IStationRepository _stations;
    private readonly IRentalRepository _rentals;
    private readonly ITransactionRepository _transactions;
    private readonly IAuditLogRepository _auditLogs;

    public DashboardService(
        IUserRepository users,
        IVehicleRepository vehicles,
        IStationRepository stations,
        IRentalRepository rentals,
        ITransactionRepository transactions,
        IAuditLogRepository auditLogs)
    {
        _users = users;
        _vehicles = vehicles;
        _stations = stations;
        _rentals = rentals;
        _transactions = transactions;
        _auditLogs = auditLogs;
    }

    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        var totalUsers = await _users.Query(tracking: false).CountAsync(ct);
        var totalVehicles = await _vehicles.Query(tracking: false).CountAsync(ct);
        var totalStations = await _stations.Query(tracking: false).Where(s => s.IsActive).CountAsync(ct);
        var activeRentals = await _rentals.Query(tracking: false)
            .Where(r => r.Status == RentalStatus.Active).CountAsync(ct);

        var completedTxns = _transactions.Query(tracking: false)
            .Where(t => t.Status == TransactionStatus.Completed);

        var totalTransactions = await completedTxns.CountAsync(ct);
        var totalRevenue = await completedTxns.SumAsync(t => t.Amount, ct);

        var todayTxns = completedTxns.Where(t => t.PaidAt >= today);
        var todayTransactions = await todayTxns.CountAsync(ct);
        var todayRevenue = await todayTxns.SumAsync(t => t.Amount, ct);

        var stations = await _stations.Query(tracking: false)
            .Where(s => s.IsActive).ToListAsync(ct);
        var stationOccupancyAvg = stations.Count > 0
            ? Math.Round(stations.Average(s => s.OccupancyPct), 1)
            : 0m;

        var vehiclesAvailable = await _vehicles.Query(tracking: false)
            .Where(v => v.Status == VehicleStatus.Available).CountAsync(ct);
        var vehiclesRented = await _vehicles.Query(tracking: false)
            .Where(v => v.Status == VehicleStatus.InTransit).CountAsync(ct);

        return new DashboardSummary(
            totalUsers, totalVehicles, totalStations, activeRentals,
            totalTransactions, totalRevenue, todayRevenue, todayTransactions,
            stationOccupancyAvg, vehiclesAvailable, vehiclesRented);
    }

    public async Task<List<DailyRevenuePoint>> GetDailyRevenueAsync(int days = 30, CancellationToken ct = default)
    {
        var from = DateTime.UtcNow.Date.AddDays(-days);
        var txns = await _transactions.Query(tracking: false)
            .Where(t => t.Status == TransactionStatus.Completed && t.PaidAt >= from)
            .ToListAsync(ct);

        return txns
            .GroupBy(t => (t.PaidAt ?? DateTime.UtcNow).ToString("yyyy-MM-dd"))
            .Select(g => new DailyRevenuePoint(g.Key, g.Sum(t => t.Amount), g.Count()))
            .OrderBy(p => p.Date)
            .ToList();
    }

    public async Task<List<PaymentMethodBreakdown>> GetPaymentMethodBreakdownAsync(CancellationToken ct = default)
    {
        var txns = await _transactions.Query(tracking: false)
            .Where(t => t.Status == TransactionStatus.Completed)
            .ToListAsync(ct);

        return txns
            .GroupBy(t => t.PaymentMethod.ToString())
            .Select(g => new PaymentMethodBreakdown(g.Key, g.Count(), g.Sum(t => t.Amount)))
            .OrderByDescending(p => p.Total)
            .ToList();
    }

    public async Task<List<RecentActivityItem>> GetRecentActivityAsync(int count = 10, CancellationToken ct = default)
    {
        var logs = await _auditLogs.Query(tracking: false)
            .OrderByDescending(a => a.LoggedAt)
            .Take(count)
            .ToListAsync(ct);

        return logs.Select(a => new RecentActivityItem(
            a.Action, a.EntityName, a.EntityId, a.LoggedAt, a.User?.Email)).ToList();
    }
}