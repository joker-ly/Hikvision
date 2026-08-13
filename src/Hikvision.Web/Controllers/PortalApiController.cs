using System.Security.Cryptography;
using System.Text;
using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Models.Enums;
using Hikvision.Web.Services.Calculation;
using Hikvision.Web.Services.Reports;
using Hikvision.Web.Services.TimeZoneSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Controllers;

/// <summary>
/// واجهة بوابة الموظفين (تطبيق الهاتف): دخول برقم الجهاز + رقم سري، مع ربط جهاز واحد
/// لكل موظف وتوكن جلسة. تعمل على الشبكة الداخلية فقط (حارس في Program).
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/portal")]
public class PortalApiController : ControllerBase
{
    private const int MaxPinAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(30);
    private static readonly PasswordHasher<Employee> Hasher = new();
    private static readonly PasswordHasher<AppUser> UserHasher = new();

    private readonly AppDbContext _db;
    private readonly IAppClock _clock;
    private readonly IAttendanceCalculationService _calc;
    private readonly IStatisticsReportService _stats;
    private readonly ILogger<PortalApiController> _logger;

    public PortalApiController(AppDbContext db, IAppClock clock,
        IAttendanceCalculationService calc, IStatisticsReportService stats,
        ILogger<PortalApiController> logger)
    {
        _db = db;
        _clock = clock;
        _calc = calc;
        _stats = stats;
        _logger = logger;
    }

    public record LoginRequest(string EmployeeNo, string Pin, string DeviceId, string? DeviceInfo);

    /// <summary>فحص وصول التطبيق للخادم + إعدادات المزامنة (لمؤقّت التطبيق).</summary>
    [HttpGet("ping")]
    public IActionResult Ping([FromServices] IConfiguration config) => Ok(new
    {
        ok = true,
        name = "HikvisionAttendance",
        syncIntervalMinutes = config.GetValue("Sync:IntervalMinutes", 15),
        syncWindowStart = config["Sync:WindowStart"] ?? "08:00",
        syncWindowEnd = config["Sync:WindowEnd"] ?? "15:00"
    });

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.EmployeeNo) || string.IsNullOrWhiteSpace(req.Pin) ||
            string.IsNullOrWhiteSpace(req.DeviceId))
            return BadRequest(new { error = "أدخل رقم الموظف والرقم السري." });

        var emp = await _db.Employees
            .Include(e => e.Group!).ThenInclude(g => g.Schedule)
            .FirstOrDefaultAsync(e => e.DeviceEmployeeNo == req.EmployeeNo.Trim() && e.IsActive, ct);

        // لا يوجد موظف بهذا الرقم → قد يكون مدير نظام يدخل باسم المستخدم وكلمة المرور
        if (emp is null)
            return await AdminLoginAsync(req, ct);

        if (emp.PinHash is null)
            return Unauthorized(new { error = "لم يُصدر لك رقم سري بعد — راجع الإدارة." });

        // قفل مؤقت بعد محاولات فاشلة متكررة
        if (emp.PinLockedUntilUtc is { } locked && locked > DateTime.UtcNow)
        {
            var mins = (int)Math.Ceiling((locked - DateTime.UtcNow).TotalMinutes);
            return StatusCode(423, new { error = $"الحساب مقفول مؤقتًا. حاول بعد {mins} دقيقة." });
        }

        if (Hasher.VerifyHashedPassword(emp, emp.PinHash, req.Pin.Trim()) == PasswordVerificationResult.Failed)
        {
            emp.PinFailedCount++;
            if (emp.PinFailedCount >= MaxPinAttempts)
            {
                emp.PinLockedUntilUtc = DateTime.UtcNow.Add(LockDuration);
                emp.PinFailedCount = 0;
            }
            await _db.SaveChangesAsync(ct);
            return Unauthorized(new { error = "بيانات الدخول غير صحيحة." });
        }

        emp.PinFailedCount = 0;
        emp.PinLockedUntilUtc = null;

        // ربط الجهاز الواحد: أول جهاز ناجح يُربط، وغيره يُرفض حتى إعادة التعيين من الإدارة
        var deviceId = req.DeviceId.Trim();
        if (string.IsNullOrEmpty(emp.DeviceId))
        {
            emp.DeviceId = deviceId.Length <= 64 ? deviceId : deviceId[..64];
            emp.DeviceInfo = req.DeviceInfo is { Length: > 0 } info
                ? (info.Length <= 300 ? info : info[..300])
                : null;
            emp.DeviceBoundAtUtc = DateTime.UtcNow;
            _logger.LogInformation("ربط جهاز جديد للموظف {No}: {Info}", emp.DeviceEmployeeNo, emp.DeviceInfo);
        }
        else if (!string.Equals(emp.DeviceId, deviceId, StringComparison.Ordinal))
        {
            await _db.SaveChangesAsync(ct); // حفظ تصفير عدّاد المحاولات
            return StatusCode(403, new { error = "هذا الحساب مرتبط بجهاز آخر. راجع الإدارة لإعادة تعيين الجهاز." });
        }

        // إصدار توكن الجلسة
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        emp.ApiTokenHash = Sha256(token);
        emp.ApiTokenExpiresUtc = DateTime.UtcNow.Add(TokenLifetime);
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            token,
            role = "employee",
            fullName = emp.FullName,
            employeeNo = emp.DeviceEmployeeNo,
            groupName = emp.Group?.Name
        });
    }

    /// <summary>
    /// دخول مدير النظام من التطبيق باسم المستخدم وكلمة المرور (بلا ربط جهاز).
    /// يُشترط أن يكون مدير نظام أو يملك صلاحية عرض الإحصائيات.
    /// </summary>
    private async Task<IActionResult> AdminLoginAsync(LoginRequest req, CancellationToken ct)
    {
        var name = req.EmployeeNo.Trim();
        var user = await _db.Users
            .Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Username == name && u.IsActive, ct);

        if (user is null)
            return Unauthorized(new { error = "بيانات الدخول غير صحيحة." });

        if (user.LockedUntilUtc is { } locked && locked > DateTime.UtcNow)
        {
            var mins = (int)Math.Ceiling((locked - DateTime.UtcNow).TotalMinutes);
            return StatusCode(423, new { error = $"الحساب مقفول مؤقتًا. حاول بعد {mins} دقيقة." });
        }

        if (UserHasher.VerifyHashedPassword(user, user.PasswordHash, req.Pin.Trim())
            == PasswordVerificationResult.Failed)
        {
            user.LoginFailedCount++;
            if (user.LoginFailedCount >= MaxPinAttempts)
            {
                user.LockedUntilUtc = DateTime.UtcNow.Add(LockDuration);
                user.LoginFailedCount = 0;
            }
            await _db.SaveChangesAsync(ct);
            return Unauthorized(new { error = "بيانات الدخول غير صحيحة." });
        }

        if (!CanViewStats(user))
            return StatusCode(403, new { error = "لا تملك صلاحية عرض الإحصائيات." });

        user.LoginFailedCount = 0;
        user.LockedUntilUtc = null;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.ApiTokenHash = Sha256(token);
        user.ApiTokenExpiresUtc = DateTime.UtcNow.Add(TokenLifetime);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("دخول مدير من التطبيق: {User}", user.Username);
        return Ok(new
        {
            token,
            role = "admin",
            fullName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName,
            employeeNo = user.Username,
            groupName = (string?)null
        });
    }

    /// <summary>مدير النظام أو من يملك صلاحية عرض الإحصائيات.</summary>
    private static bool CanViewStats(AppUser user) =>
        user.IsAdmin ||
        user.Permissions.Any(p => p.Module == AppModule.Statistics && p.CanView);

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var admin = await AuthAdminAsync(ct);
        if (admin is not null)
        {
            admin.ApiTokenHash = null;
            admin.ApiTokenExpiresUtc = null;
            await _db.SaveChangesAsync(ct);
            return Ok(new { ok = true });
        }

        var emp = await AuthAsync(ct);
        if (emp is null) return Unauthorized(new { error = "انتهت الجلسة." });
        emp.ApiTokenHash = null;
        emp.ApiTokenExpiresUtc = null;
        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var emp = await AuthAsync(ct);
        if (emp is null) return Unauthorized(new { error = "انتهت الجلسة." });

        var s = emp.Group?.Schedule;
        return Ok(new
        {
            fullName = emp.FullName,
            employeeNo = emp.DeviceEmployeeNo,
            financialNo = emp.FinancialNo,
            groupName = emp.Group?.Name,
            scheduleStart = s?.StartTime?.ToString("HH:mm"),
            scheduleEnd = s?.EndTime?.ToString("HH:mm"),
            lateGraceMinutes = s?.LateGraceMinutes ?? 0,
            deviceBoundAt = emp.DeviceBoundAtUtc is { } b
                ? _clock.ToLocal(new DateTimeOffset(DateTime.SpecifyKind(b, DateTimeKind.Utc))).ToString("yyyy-MM-dd")
                : null,
            deviceInfo = emp.DeviceInfo
        });
    }

    [HttpGet("today")]
    public async Task<IActionResult> Today(CancellationToken ct)
    {
        var emp = await AuthAsync(ct);
        if (emp is null) return Unauthorized(new { error = "انتهت الجلسة." });

        var now = _clock.Now;
        var today = now.Date;
        var tomorrow = today.AddDays(1);

        var punches = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.EmployeeId == emp.Id && r.EventTime >= today && r.EventTime < tomorrow)
            .OrderBy(r => r.EventTime)
            .Select(r => new { r.EventTime, r.Direction, r.Source })
            .ToListAsync(ct);

        var s = emp.Group?.Schedule;
        var isWorkingDay = s?.IsWorkingDay(today.DayOfWeek)
            ?? (today.DayOfWeek != DayOfWeek.Friday && today.DayOfWeek != DayOfWeek.Saturday);

        DateTime? firstIn = punches.Count > 0 ? punches[0].EventTime : null;
        DateTime? lastOut = punches.Count > 1 ? punches[^1].EventTime : null;

        int lateMinutes = 0;
        if (firstIn is { } fi && s?.StartTime is { } start)
        {
            var allowed = start.ToTimeSpan().Add(TimeSpan.FromMinutes(s.LateGraceMinutes));
            if (fi.TimeOfDay > allowed)
                lateMinutes = (int)Math.Round((fi.TimeOfDay - allowed).TotalMinutes);
        }

        return Ok(new
        {
            date = today.ToString("yyyy-MM-dd"),
            isWorkingDay,
            firstIn = firstIn?.ToString("HH:mm"),
            lastOut = lastOut?.ToString("HH:mm"),
            lateMinutes,
            punchCount = punches.Count
        });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        var emp = await AuthAsync(ct);
        if (emp is null) return Unauthorized(new { error = "انتهت الجلسة." });

        var now = _clock.Now;
        var y = year ?? now.Year;
        var m = Math.Clamp(month ?? now.Month, 1, 12);

        var from = new DateOnly(y, m, 1);
        var to = new DateOnly(y, m, DateTime.DaysInMonth(y, m));
        var today = DateOnly.FromDateTime(now);
        if (to > today) to = today;
        if (from > today)
            return Ok(new { year = y, month = m, periodDays = 0 });

        var days = await _calc.CalculateAsync(emp.Id, from, to, ct);
        var present = days.Count(d => d.IsPresent);

        return Ok(new
        {
            year = y,
            month = m,
            periodDays = days.Count,
            presentDays = present,
            absentDays = days.Count - present,
            lateCount = days.Count(d => d.IsLate),
            lateMinutes = days.Sum(d => d.LateMinutes),
            leaveDays = days.Count(d => d.ManualTypeApplied == ManualAttendanceType.Leave),
            unpaidLeaveDays = days.Count(d => d.IsUnpaidLeave),
            missionDays = days.Count(d => d.ManualTypeApplied == ManualAttendanceType.WorkMission),
            workedHours = Math.Round(days.Sum(d => d.WorkedHours), 1),
            presenceRate = days.Count > 0 ? Math.Round(present * 100m / days.Count, 1) : 0
        });
    }

    [HttpGet("records")]
    public async Task<IActionResult> Records([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        var emp = await AuthAsync(ct);
        if (emp is null) return Unauthorized(new { error = "انتهت الجلسة." });

        var now = _clock.Now;
        var y = year ?? now.Year;
        var m = Math.Clamp(month ?? now.Month, 1, 12);
        var fromDt = new DateTime(y, m, 1);
        var toDt = fromDt.AddMonths(1);

        var records = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.EmployeeId == emp.Id && r.EventTime >= fromDt && r.EventTime < toDt)
            .OrderByDescending(r => r.EventTime)
            .Select(r => new
            {
                date = r.EventTime.ToString("yyyy-MM-dd"),
                time = r.EventTime.ToString("HH:mm"),
                direction = r.Direction.ToString(),
                source = r.Source.ToString(),
                manualType = r.ManualType.ToString()
            })
            .ToListAsync(ct);

        return Ok(new { year = y, month = m, count = records.Count, records });
    }

    // ==================== واجهة الأدمن (تقارير وإحصائيات) ====================

    /// <summary>المؤشرات العامة وقوائم الأعلى وملخص المجموعات لشهر محدد.</summary>
    [HttpGet("admin/stats")]
    public async Task<IActionResult> AdminStats(
        [FromQuery] int? groupId, [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        var user = await AuthAdminAsync(ct);
        if (user is null) return Unauthorized(new { error = "انتهت الجلسة." });

        var (from, to, y, m, hasData) = ResolveMonth(year, month);
        if (!hasData) return Ok(new { year = y, month = m, empty = true });

        var stats = await _stats.BuildAsync(groupId, from, to, ct);
        var k = stats.Kpis;

        // قوائم الأعلى — نفس منطق صفحة الإحصائيات في الويب
        var topAbsent = stats.Employees.Where(e => e.DaysAbsent > 0)
            .OrderByDescending(e => e.AbsenceRate).ThenByDescending(e => e.DaysAbsent).Take(10);
        var topLate = stats.Employees.Where(e => e.LateCount > 0)
            .OrderByDescending(e => e.TotalLateMinutes).Take(10);
        // المعفيون من البصمة يُستبعدون من قائمة الانضباط (حضورهم تلقائي)
        var topPresent = stats.Employees.Where(e => !e.IsExempt)
            .OrderByDescending(e => e.PresenceRate).ThenBy(e => e.DaysAbsent).Take(10);

        return Ok(new
        {
            year = y,
            month = m,
            empty = false,
            kpis = new
            {
                k.EmployeeCount,
                k.AvgPresenceRate,
                k.TotalAbsentDays,
                k.TotalLeaveDays,
                k.TotalUnpaidLeaveDays,
                k.TotalMissionDays,
                k.TotalPermittedExits,
                k.TotalLateCount,
                k.TotalLateMinutes
            },
            topAbsent = topAbsent.Select(e => new
            {
                e.EmployeeId, e.FullName, e.GroupName, e.DaysAbsent, e.AbsenceRate
            }),
            topLate = topLate.Select(e => new
            {
                e.EmployeeId, e.FullName, e.GroupName, e.LateCount, e.TotalLateMinutes
            }),
            topPresent = topPresent.Select(e => new
            {
                e.EmployeeId, e.FullName, e.GroupName, e.DaysPresent, e.PresenceRate
            }),
            groups = stats.Groups.Select(g => new
            {
                g.GroupName, g.EmployeeCount, g.AvgPresenceRate, g.TotalAbsentDays,
                g.TotalLateCount, g.TotalLateMinutes
            }),
            daily = stats.Daily.Select(dd => new
            {
                date = dd.Date.ToString("yyyy-MM-dd"),
                dd.DayName,
                dd.IsHoliday,
                // يوم عطلة للجميع: لا حضور ولا غياب ولا نسبة
                isOff = dd.IsHoliday || dd.IsOffForAll,
                dd.Required,
                dd.Present,
                dd.Absent,
                dd.Late,
                dd.PresentOnOff,
                dd.OffDay,
                dd.Exempt,
                dd.PresenceRate
            })
        });
    }

    /// <summary>بحث الموظفين بالاسم أو الرقم المالي أو رقم البصمة.</summary>
    [HttpGet("admin/employees")]
    public async Task<IActionResult> AdminEmployees(
        [FromQuery] string? q, [FromQuery] int? groupId, CancellationToken ct)
    {
        var user = await AuthAdminAsync(ct);
        if (user is null) return Unauthorized(new { error = "انتهت الجلسة." });

        var query = _db.Employees.Include(e => e.Group).AsNoTracking().Where(e => e.IsActive);
        if (groupId.HasValue)
            query = query.Where(e => e.EmployeeGroupId == groupId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(e =>
                (e.FinancialNo != null && e.FinancialNo.Contains(term)) ||
                e.FullName.Contains(term) ||
                e.DeviceEmployeeNo.Contains(term));
        }

        var list = await query.OrderBy(e => e.FullName).Take(50)
            .Select(e => new
            {
                id = e.Id,
                fullName = e.FullName,
                financialNo = e.FinancialNo,
                employeeNo = e.DeviceEmployeeNo,
                groupName = e.Group != null ? e.Group.Name : null
            })
            .ToListAsync(ct);

        return Ok(new { count = list.Count, employees = list });
    }

    /// <summary>إحصائيات موظف واحد لشهر محدد + آخر بصماته.</summary>
    [HttpGet("admin/employee/{id:int}")]
    public async Task<IActionResult> AdminEmployee(
        int id, [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        var user = await AuthAdminAsync(ct);
        if (user is null) return Unauthorized(new { error = "انتهت الجلسة." });

        var emp = await _db.Employees.Include(e => e.Group!).ThenInclude(g => g.Schedule)
            .AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (emp is null) return NotFound(new { error = "الموظف غير موجود." });

        var (from, to, y, m, hasData) = ResolveMonth(year, month);

        var info = new
        {
            id = emp.Id,
            fullName = emp.FullName,
            financialNo = emp.FinancialNo,
            employeeNo = emp.DeviceEmployeeNo,
            groupName = emp.Group?.Name,
            isExempt = emp.Group?.IsFingerprintExempt ?? false,
            scheduleStart = emp.Group?.Schedule?.StartTime?.ToString("HH:mm"),
            scheduleEnd = emp.Group?.Schedule?.EndTime?.ToString("HH:mm"),
            exemptionDate = emp.ExemptionDate?.ToString("yyyy-MM-dd")
        };

        if (!hasData)
            return Ok(new { year = y, month = m, employee = info, periodDays = 0, records = Array.Empty<object>() });

        var days = await _calc.CalculateAsync(emp.Id, from, to, ct);
        var present = days.Count(d => d.IsPresent);

        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var records = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.EmployeeId == emp.Id && r.EventTime >= fromDt && r.EventTime < toDt)
            .OrderByDescending(r => r.EventTime).Take(60)
            .Select(r => new
            {
                date = r.EventTime.ToString("yyyy-MM-dd"),
                time = r.EventTime.ToString("HH:mm"),
                direction = r.Direction.ToString(),
                source = r.Source.ToString()
            })
            .ToListAsync(ct);

        return Ok(new
        {
            year = y,
            month = m,
            employee = info,
            periodDays = days.Count,
            presentDays = present,
            absentDays = days.Count - present,
            lateCount = days.Count(d => d.IsLate),
            lateMinutes = days.Sum(d => d.LateMinutes),
            leaveDays = days.Count(d => d.ManualTypeApplied == ManualAttendanceType.Leave),
            unpaidLeaveDays = days.Count(d => d.IsUnpaidLeave),
            missionDays = days.Count(d => d.ManualTypeApplied == ManualAttendanceType.WorkMission),
            permittedExits = days.Count(d => d.HasPermittedExit),
            workedHours = Math.Round(days.Sum(d => d.WorkedHours), 1),
            presenceRate = days.Count > 0 ? Math.Round(present * 100m / days.Count, 1) : 0,
            records
        });
    }

    /// <summary>قائمة المجموعات لفلتر واجهة الأدمن.</summary>
    [HttpGet("admin/groups")]
    public async Task<IActionResult> AdminGroups(CancellationToken ct)
    {
        var user = await AuthAdminAsync(ct);
        if (user is null) return Unauthorized(new { error = "انتهت الجلسة." });

        var groups = await _db.EmployeeGroups.AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new { id = g.Id, name = g.Name })
            .ToListAsync(ct);
        return Ok(new { groups });
    }

    /// <summary>حدود الشهر المطلوب (مقيّدًا باليوم الحالي).</summary>
    private (DateOnly from, DateOnly to, int year, int month, bool hasData) ResolveMonth(
        int? year, int? month)
    {
        var now = _clock.Now;
        var y = year ?? now.Year;
        var m = Math.Clamp(month ?? now.Month, 1, 12);

        var from = new DateOnly(y, m, 1);
        var to = new DateOnly(y, m, DateTime.DaysInMonth(y, m));
        var today = DateOnly.FromDateTime(now);
        if (to > today) to = today;
        return (from, to, y, m, from <= today);
    }

    private async Task<Employee?> AuthAsync(CancellationToken ct)
    {
        var hash = TokenHashFromHeader();
        if (hash is null) return null;

        var emp = await _db.Employees
            .Include(e => e.Group!).ThenInclude(g => g.Schedule)
            .FirstOrDefaultAsync(e => e.ApiTokenHash == hash && e.IsActive, ct);

        if (emp is null || emp.ApiTokenExpiresUtc is not { } exp || exp < DateTime.UtcNow)
            return null;
        return emp;
    }

    /// <summary>مصادقة مدير من التطبيق: توكن سارٍ + صلاحية عرض الإحصائيات.</summary>
    private async Task<AppUser?> AuthAdminAsync(CancellationToken ct)
    {
        var hash = TokenHashFromHeader();
        if (hash is null) return null;

        var user = await _db.Users
            .Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.ApiTokenHash == hash && u.IsActive, ct);

        if (user is null || user.ApiTokenExpiresUtc is not { } exp || exp < DateTime.UtcNow)
            return null;
        // الصلاحية تُفحص في كل طلب (قد تُسحب بعد إصدار التوكن)
        return CanViewStats(user) ? user : null;
    }

    private string? TokenHashFromHeader()
    {
        var auth = Request.Headers.Authorization.ToString();
        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = auth["Bearer ".Length..].Trim();
        return token.Length == 0 ? null : Sha256(token);
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
