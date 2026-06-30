using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Models.Enums;
using Hikvision.Web.Services.Audit;
using Hikvision.Web.Services.TimeZoneSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Controllers;

public class AttendanceController : Controller
{
    private readonly AppDbContext _db;
    private readonly IAppClock _clock;
    private readonly IAuditLogger _audit;

    public AttendanceController(AppDbContext db, IAppClock clock, IAuditLogger audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
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

    // حذف سجل يدوي واحد (لا يُسمح بحذف سجلات الجهاز من هنا)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteManual(int id, int? employeeId, DateOnly? from, DateOnly? to, AttendanceSource? source)
    {
        var rec = await _db.AttendanceRecords.FirstOrDefaultAsync(r => r.Id == id);
        if (rec is null || rec.Source != AttendanceSource.Manual)
        {
            TempData["Error"] = "لا يمكن حذف هذا السجل (السجلات اليدوية فقط).";
        }
        else
        {
            _db.AttendanceRecords.Remove(rec);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("حذف سجل يدوي",
                $"موظف #{rec.EmployeeId}، {rec.EventTime:yyyy-MM-dd HH:mm}، {rec.ManualType}.");
            TempData["Success"] = "تم حذف السجل اليدوي.";
        }
        return RedirectToAction(nameof(Index), new { employeeId, from, to, source });
    }

    // حذف كل السجلات اليدوية ضمن التصفية الحالية (الموظف + المدى الزمني)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteManualFiltered(int? employeeId, DateOnly? from, DateOnly? to, AttendanceSource? source)
    {
        from ??= DateOnly.FromDateTime(_clock.Now).AddDays(-30);
        to ??= DateOnly.FromDateTime(_clock.Now);
        var fromDt = from.Value.ToDateTime(TimeOnly.MinValue);
        var toDt = to.Value.ToDateTime(TimeOnly.MaxValue);

        var query = _db.AttendanceRecords
            .Where(r => r.Source == AttendanceSource.Manual && r.EventTime >= fromDt && r.EventTime <= toDt);
        if (employeeId.HasValue) query = query.Where(r => r.EmployeeId == employeeId.Value);

        var deleted = await query.ExecuteDeleteAsync();
        await _audit.LogAsync("حذف سجلات يدوية",
            $"حُذف {deleted} سجلًا يدويًا، الفترة {from:yyyy-MM-dd} إلى {to:yyyy-MM-dd}" +
            (employeeId.HasValue ? $"، موظف #{employeeId}" : "، كل الموظفين") + ".");
        TempData["Success"] = $"تم حذف {deleted} سجلًا يدويًا ضمن التصفية.";

        return RedirectToAction(nameof(Index), new { employeeId, from, to, source });
    }
}
