using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaigonRide.Services.Dashboard;

namespace SaigonRide.Web.Controllers;

/// <summary>
/// Interactive Admin Dashboard for system monitoring (Tier 4 requirement).
/// Provides real-time KPIs, revenue charts, payment breakdowns, and activity feed.
/// All endpoints are JSON-returning for AJAX consumption by Chart.js on the client.
/// </summary>
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    /// <summary>Main dashboard page (server-rendered shell; data loaded via AJAX).</summary>
    [HttpGet]
    public IActionResult Index() => View();

    /// <summary>KPI summary cards.</summary>
    [HttpGet]
    public async Task<IActionResult> Summary(CancellationToken ct)
        => Json(await _dashboard.GetSummaryAsync(ct));

    /// <summary>Daily revenue line chart data.</summary>
    [HttpGet]
    public async Task<IActionResult> RevenueChart(int days = 30, CancellationToken ct = default)
        => Json(await _dashboard.GetDailyRevenueAsync(days, ct));

    /// <summary>Payment method pie chart data.</summary>
    [HttpGet]
    public async Task<IActionResult> PaymentBreakdown(CancellationToken ct)
        => Json(await _dashboard.GetPaymentMethodBreakdownAsync(ct));

    /// <summary>Recent activity feed.</summary>
    [HttpGet]
    public async Task<IActionResult> Activity(int count = 10, CancellationToken ct = default)
        => Json(await _dashboard.GetRecentActivityAsync(count, ct));
}