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

    private (DateOnly from, DateOnly to) DefaultMonth()
    {
        var now = _clock.Now;
        return (new DateOnly(now.Year, now.Month, 1), DateOnly.FromDateTime(now));
    }

    [HttpGet]
    public async Task<IActionResult> Payroll(int? groupId, DateOnly? from, DateOnly? to)
    {
        var (df, dt) = DefaultMonth();
        from ??= df;
        to ??= dt;

        await PopulateGroups(groupId);
        var report = await _reports.BuildAsync(groupId, from.Value, to.Value);
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
