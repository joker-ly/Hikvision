using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Models.Enums;
using Hikvision.Web.Services.Audit;
using Hikvision.Web.Services.TimeZoneSupport;
using Hikvision.Web.ViewModels.ManualAttendance;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Controllers;

public class ManualAttendanceController : Controller
{
    private readonly AppDbContext _db;
    private readonly IAppClock _clock;
    private readonly IAuditLogger _audit;

    public ManualAttendanceController(AppDbContext db, IAppClock clock, IAuditLogger audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    private async Task PopulateEmployees(int? selected = null)
        => ViewBag.Employees = new SelectList(
            await _db.Employees.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(),
            "Id", "FullName", selected);

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateEmployees();
        var today = DateOnly.FromDateTime(_clock.Now);
        return View(new ManualEntryViewModel { FromDate = today, ToDate = today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ManualEntryViewModel model)
    {
        if (model.ManualType == ManualAttendanceType.None)
            ModelState.AddModelError(nameof(model.ManualType), "اختر نوع السجل اليدوي.");
        // الإعفاء يستخدم "من تاريخ" فقط كتاريخ إعفاء؛ باقي الأنواع تتطلب فترة صحيحة
        if (model.ManualType != ManualAttendanceType.Exemption && model.ToDate < model.FromDate)
            ModelState.AddModelError(nameof(model.ToDate), "تاريخ النهاية يجب أن يكون بعد تاريخ البداية.");

        if (!ModelState.IsValid)
        {
            await PopulateEmployees(model.EmployeeId);
            return View(model);
        }

        var employee = await _db.Employees
            .Include(e => e.Group!).ThenInclude(g => g.Schedule)
            .FirstOrDefaultAsync(e => e.Id == model.EmployeeId);
        if (employee is null)
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "الموظف غير موجود.");
            await PopulateEmployees(model.EmployeeId);
            return View(model);
        }

        // الإعفاء: يُضبط تاريخ الإعفاء على الموظف ولا يُحتسب له أي حضور بعده (بلا توليد بصمات)
        if (model.ManualType == ManualAttendanceType.Exemption)
        {
            employee.ExemptionDate = model.FromDate;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("إعفاء موظف",
                $"موظف #{employee.Id} ({employee.FullName})، تاريخ الإعفاء {model.FromDate} — لا يُحتسب حضور بعده. {model.Note}");
            TempData["Success"] =
                $"تم إعفاء {employee.FullName} اعتبارًا من {model.FromDate:yyyy-MM-dd}؛ لن يُحتسب له حضور بعد هذا التاريخ.";
            return RedirectToAction("Index", "Attendance", new { source = AttendanceSource.Manual });
        }

        var schedule = employee.Group?.Schedule;

        // السجلات الموجودة لتفادي التكرار
        var fromDt = model.FromDate.ToDateTime(TimeOnly.MinValue);
        var toDt = model.ToDate.ToDateTime(TimeOnly.MaxValue);
        var existing = (await _db.AttendanceRecords
            .Where(r => r.EmployeeId == model.EmployeeId && r.Source == AttendanceSource.Manual &&
                        r.EventTime >= fromDt && r.EventTime <= toDt)
            .Select(r => new { r.EventTime, r.Direction })
            .ToListAsync())
            .Select(x => (x.EventTime, x.Direction)).ToHashSet();

        var toAdd = new List<AttendanceRecord>();
        int dayCount = 0;

        for (var day = model.FromDate; day <= model.ToDate; day = day.AddDays(1))
        {
            if (model.WorkingDaysOnly && schedule is not null && !schedule.IsWorkingDay(day.DayOfWeek))
                continue;

            if (model.ManualType == ManualAttendanceType.PermittedExit)
            {
                // إذن خروج مبكر: سجل واحد عند وقت الخروج
                AddIfNew(toAdd, existing, model.EmployeeId, day.ToDateTime(model.EndTime),
                    PunchDirection.CheckOut, model.ManualType, model.Note);
            }
            else
            {
                // إجازة/مهمة/إنجاز: بصمتا دخول وخروج آليتان
                AddIfNew(toAdd, existing, model.EmployeeId, day.ToDateTime(model.StartTime),
                    PunchDirection.CheckIn, model.ManualType, model.Note);
                AddIfNew(toAdd, existing, model.EmployeeId, day.ToDateTime(model.EndTime),
                    PunchDirection.CheckOut, model.ManualType, model.Note);
            }
            dayCount++;
        }

        if (toAdd.Count > 0)
        {
            _db.AttendanceRecords.AddRange(toAdd);
            await _db.SaveChangesAsync();
        }

        await _audit.LogAsync("إدخال حضور يدوي",
            $"موظف #{model.EmployeeId} ({employee.FullName})، النوع: {model.ManualType}، " +
            $"من {model.FromDate} إلى {model.ToDate}، {dayCount} يوم، {toAdd.Count} سجل.");

        TempData["Success"] = $"تمت إضافة {toAdd.Count} سجلًا يدويًا عبر {dayCount} يوم وتوثيقها.";
        return RedirectToAction("Index", "Attendance", new { source = AttendanceSource.Manual });
    }

    private static void AddIfNew(
        List<AttendanceRecord> toAdd, HashSet<(DateTime, PunchDirection)> existing,
        int empId, DateTime when, PunchDirection dir, ManualAttendanceType type, string note)
    {
        var key = (when, dir);
        if (existing.Contains(key) || toAdd.Any(r => r.EventTime == when && r.Direction == dir))
            return;
        toAdd.Add(new AttendanceRecord
        {
            EmployeeId = empId,
            EventTime = when,
            Source = AttendanceSource.Manual,
            Direction = dir,
            ManualType = type,
            Note = note,
            CreatedAtUtc = DateTime.UtcNow
        });
    }
}
