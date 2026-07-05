using Hikvision.Web.Data;
using Hikvision.Web.Services.Reports;
using Hikvision.Web.Services.TimeZoneSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Controllers;

public class ReportsController : Controller
{
    private readonly IPayrollReportService _reports;
    private readonly AppDbContext _db;
    private readonly IAppClock _clock;

    public ReportsController(IPayrollReportService reports, AppDbContext db, IAppClock clock)
    {
        _reports = reports;
        _db = db;
        _clock = clock;
    }

    private async Task PopulateGroups(int? selected)
        => ViewBag.Groups = new SelectList(await _db.EmployeeGroups.AsNoTracking().ToListAsync(), "Id", "Name", selected);

    /// <summary>تحويل قيمة حقل شهر (yyyy-MM) إلى أول يوم في الشهر.</summary>
    private static DateOnly? ParseMonth(string? ym) =>
        DateOnly.TryParseExact((ym ?? string.Empty) + "-01", "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;

    [HttpGet]
    public async Task<IActionResult> Payroll(int? groupId, string? fromMonth, string? toMonth)
    {
        var now = _clock.Now;
        var today = DateOnly.FromDateTime(now);

        // الاختيار بالشهور: من أول يوم في "من شهر" إلى آخر يوم في "إلى شهر"
        var fm = ParseMonth(fromMonth) ?? new DateOnly(now.Year, now.Month, 1);
        var tm = ParseMonth(toMonth) ?? fm;
        if (tm < fm) tm = fm;

        var from = fm;
        var to = tm.AddMonths(1).AddDays(-1);
        // لا نتجاوز اليوم الحالي حتى لا تُحتسب الأيام المقبلة غيابًا
        if (to > today) to = today;

        await PopulateGroups(groupId);
        var report = await _reports.BuildAsync(groupId, from, to);
        return View(report);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(int? groupId, DateOnly from, DateOnly to)
    {
        var report = await _reports.BuildAsync(groupId, from, to);
        var bytes = _reports.ExportCsv(report);
        return File(bytes, "text/csv", $"payroll_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportXlsx(int? groupId, DateOnly from, DateOnly to)
    {
        var report = await _reports.BuildAsync(groupId, from, to);
        var bytes = _reports.ExportXlsx(report);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"payroll_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx");
    }
}
