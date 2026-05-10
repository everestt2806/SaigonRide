using SaigonRide.Services.Reporting;

namespace SaigonRide.Web.Models;

public class RevenueReportViewModel
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int? CategoryId { get; set; }
    public IReadOnlyList<RevenueRow> Rows { get; set; } = Array.Empty<RevenueRow>();
}
