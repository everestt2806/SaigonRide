using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonRide.Services.Reporting;
using SaigonRide.Web.Models;

namespace SaigonRide.Web.Controllers;

/// <summary>
/// RPT-01 Revenue by category (Minh) and RPT-02 Station utilisation (Sơn).
/// Both feed Chart.js views; CSV export is provided for ad-hoc analysis.
/// </summary>
[Authorize(Roles = "Admin")]
public class ReportController : Controller
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService) => _reportService = reportService;

    [HttpGet]
    public async Task<IActionResult> Revenue(DateTime? from, DateTime? to, int? categoryId)
    {
        var fromUtc = (from ?? DateTime.UtcNow.Date.AddDays(-30)).ToUniversalTime();
        var toUtc = (to ?? DateTime.UtcNow).ToUniversalTime();
        var rows = await _reportService.GetRevenueByCategoryAsync(fromUtc, toUtc, categoryId);
        var vm = new RevenueReportViewModel
        {
            FromDate = fromUtc,
            ToDate = toUtc,
            CategoryId = categoryId,
            Rows = rows
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> RevenueCsv(DateTime? from, DateTime? to, int? categoryId)
    {
        var fromUtc = (from ?? DateTime.UtcNow.Date.AddDays(-30)).ToUniversalTime();
        var toUtc = (to ?? DateTime.UtcNow).ToUniversalTime();
        var rows = await _reportService.GetRevenueByCategoryAsync(fromUtc, toUtc, categoryId);

        var sb = new StringBuilder();
        sb.AppendLine("CategoryId,CategoryName,TripCount,GrossRevenue,TotalDiscount,NetRevenue");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                row.CategoryId.ToString(CultureInfo.InvariantCulture),
                Csv(row.CategoryName),
                row.TripCount.ToString(CultureInfo.InvariantCulture),
                row.GrossRevenue.ToString("F2", CultureInfo.InvariantCulture),
                row.TotalDiscount.ToString("F2", CultureInfo.InvariantCulture),
                row.NetRevenue.ToString("F2", CultureInfo.InvariantCulture)
            }));
        }
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"saigonride-revenue-{fromUtc:yyyyMMdd}-{toUtc:yyyyMMdd}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> StationUtilisation()
    {
        var rows = await _reportService.GetStationUtilisationAsync();
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> StationUtilisationData()
    {
        var rows = await _reportService.GetStationUtilisationAsync();
        return Json(rows);
    }

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
