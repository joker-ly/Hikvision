using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Models.Enums;
using Hikvision.Web.Services.TimeZoneSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Controllers;

public class AttendanceController : Controller
{
    private readonly AppDbContext _db;
    private readonly IAppClock _clock;

    public AttendanceController(AppDbContext db, IAppClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IActionResult> Index(int? employeeId, DateOnly? from, DateOnly? to, AttendanceSource? source)
    {
        from ??= DateOnly.FromDateTime(_clock.Now).AddDays(-30);
        to ??= DateOnly.FromDateTime(_clock.Now);

        var fromDt = from.Value.ToDateTime(TimeOnly.MinValue);
        var toDt = to.Value.ToDateTime(TimeOnly.MaxValue);

        var query = _db.AttendanceRecords
            .Include(r => r.Employee)
            .Where(r => r.EventTime >= fromDt && r.EventTime <= toDt);

        if (employeeId.HasValue) query = query.Where(r => r.EmployeeId == employeeId.Value);
        if (source.HasValue) query = query.Where(r => r.Source == source.Value);

        var records = await query.OrderByDescending(r => r.EventTime)
            .AsNoTracking().Take(1000).ToListAsync();

        ViewBag.Employees = new SelectList(
            await _db.Employees.AsNoTracking().OrderBy(e => e.FullName).ToListAsync(),
            "Id", "FullName", employeeId);
        ViewBag.EmployeeId = employeeId;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Source = source;

        return View(records);
    }
}
