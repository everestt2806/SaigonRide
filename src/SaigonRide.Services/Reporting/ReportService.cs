using Microsoft.EntityFrameworkCore;
using SaigonRide.Data.Repositories;
using SaigonRide.Domain.Enums;

namespace SaigonRide.Services.Reporting;

public interface IReportService
{
    Task<List<RevenueRow>> GetRevenueByCategoryAsync(DateTime fromUtc, DateTime toUtc, int? categoryId = null, CancellationToken ct = default);
    Task<List<StationUtilisationRow>> GetStationUtilisationAsync(CancellationToken ct = default);
}

public record RevenueRow(int CategoryId, string CategoryName, int TripCount, decimal GrossRevenue, decimal TotalDiscount, decimal NetRevenue);
public record StationUtilisationRow(int StationId, string StationName, int Capacity, int CurrentCount, decimal OccupancyPct, StationStatus Status);

/// <summary>
/// Aggregations for RPT-01 (Revenue by category) and RPT-02 (Station
/// utilisation). RPT-01 calls <c>IgnoreQueryFilters</c> so historical
/// (decommissioned) categories still appear in revenue charts.
/// </summary>
public class ReportService : IReportService
{
    private readonly ITransactionRepository _transactions;
    private readonly IStationRepository _stations;
    private readonly IVehicleCategoryRepository _categories;

    public ReportService(ITransactionRepository transactions, IStationRepository stations, IVehicleCategoryRepository categories)
    {
        _transactions = transactions;
        _stations = stations;
        _categories = categories;
    }

    public async Task<List<RevenueRow>> GetRevenueByCategoryAsync(DateTime fromUtc, DateTime toUtc, int? categoryId = null, CancellationToken ct = default)
    {
        var query = _transactions.Query(tracking: false, ignoreFilters: true)
            .Include(t => t.Rental!).ThenInclude(r => r.Vehicle!).ThenInclude(v => v.Category!)
            .Where(t => t.Status == TransactionStatus.Completed && t.PaidAt >= fromUtc && t.PaidAt <= toUtc);

        // Group on the server side to avoid loading all rows into memory
        var grouped = await query
            .Where(t => t.Rental != null && t.Rental.Vehicle != null && t.Rental.Vehicle.Category != null)
            .Where(t => !categoryId.HasValue || t.Rental!.Vehicle!.Category!.Id == categoryId.Value)
            .GroupBy(t => new { t.Rental!.Vehicle!.Category!.Id, t.Rental.Vehicle.Category.Name })
            .Select(g => new RevenueRow(
                g.Key.Id,
                g.Key.Name,
                g.Count(),
                g.Sum(x => x.Amount + x.Discount),
                g.Sum(x => x.Discount),
                g.Sum(x => x.Amount)))
            .ToListAsync(ct);

        // Add zero-revenue categories so the report is complete
        var allCategories = await _categories.Query(tracking: false, ignoreFilters: true).ToListAsync(ct);
        foreach (var c in allCategories.Where(c => !grouped.Any(r => r.CategoryId == c.Id)))
        {
            if (categoryId.HasValue && c.Id != categoryId.Value) continue;
            grouped.Add(new RevenueRow(c.Id, c.Name, 0, 0m, 0m, 0m));
        }
        return grouped.OrderByDescending(r => r.NetRevenue).ToList();
    }

    public async Task<List<StationUtilisationRow>> GetStationUtilisationAsync(CancellationToken ct = default)
    {
        var stations = await _stations.Query(tracking: false).Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(ct);
        return stations.Select(s => new StationUtilisationRow(
            StationId: s.Id,
            StationName: s.Name,
            Capacity: s.Capacity,
            CurrentCount: s.CurrentCount,
            OccupancyPct: Math.Round(s.OccupancyPct, 1),
            Status: s.Status)).ToList();
    }
}
