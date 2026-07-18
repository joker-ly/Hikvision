using Hikvision.Web.Data;
using Hikvision.Web.Models.Enums;
using Hikvision.Web.Services.Sync;
using Hikvision.Web.Services.TimeZoneSupport;
using Hikvision.Web.ViewModels.Home;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hikvision.Web.Services.Auth;

namespace Hikvision.Web.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly IAttendanceSyncService _sync;
    private readonly IAppClock _clock;

    public HomeController(AppDbContext db, IAttendanceSyncService sync, IAppClock clock)
    {
        _db = db;
        _sync = sync;
        _clock = clock;
    }

    [Perm(AppModule.Dashboard, PermAction.View)]
    public async Task<IActionResult> Index()
    {
        var today = _clock.Now.Date;
        var tomorrow = today.AddDays(1);
        var last30 = today.AddDays(-30);
        var last15 = today.AddDays(-15);

        var vm = new DashboardViewModel
        {
            Today = today,
            EmployeeCount = await _db.Employees.CountAsync(),
            GroupCount = await _db.EmployeeGroups.CountAsync(),
            AttendanceRecordCount = await _db.AttendanceRecords.CountAsync(),
            TodayRecordCount = await _db.AttendanceRecords
                .CountAsync(r => r.EventTime >= today && r.EventTime < tomorrow),
            LastSync = await _sync.GetLastSyncAsync()
        };

        // الموظفون النشطون مع مجموعاتهم ووردياتهم
        var employees = await _db.Employees
            .Include(e => e.Group!).ThenInclude(g => g.Schedule)
            .Where(e => e.IsActive)
            .AsNoTracking().ToListAsync();

        // سجلات اليوم (جهاز + يدوي) مع المصدر
        var todayRecords = await _db.AttendanceRecords
            .Where(r => r.EventTime >= today && r.EventTime < tomorrow)
            .Select(r => new { r.EmployeeId, r.EventTime, r.Direction, r.Source })
            .AsNoTracking().ToListAsync();

        // إحصاءات الحضور/التأخير/ما زال بالداخل تُبنى من بصمات الجهاز فقط
        var deviceByEmp = todayRecords
            .Where(r => r.Source == AttendanceSource.Device)
            .GroupBy(r => r.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.EventTime).ToList());

        // الغياب يُحدَّد بغياب أي سجل اليوم (جهاز أو يدوي) — فمن له إجازة/مهمة يدوية لا يُعد غائبًا
        var anyRecordToday = todayRecords.Select(r => r.EmployeeId).ToHashSet();

        // الموظفون الذين لديهم بصمة جهاز خلال آخر 30 يومًا
        var recentIds = (await _db.AttendanceRecords
            .Where(r => r.EventTime >= last30 && r.Source == AttendanceSource.Device)
            .Select(r => r.EmployeeId)
            .Distinct().ToListAsync()).ToHashSet();

        // الموظفون الذين لديهم بصمة جهاز خلال آخر 15 يومًا
        var recentIds15 = (await _db.AttendanceRecords
            .Where(r => r.EventTime >= last15 && r.Source == AttendanceSource.Device)
            .Select(r => r.EmployeeId)
            .Distinct().ToListAsync()).ToHashSet();

        foreach (var e in employees.OrderBy(e => e.FullName))
        {
            var groupName = e.Group?.Name ?? "—";
            var schedule = e.Group?.Schedule;
            var isWorkingDay = schedule?.IsWorkingDay(today.DayOfWeek) ?? true;

            // موظف معفى (انتهت خدمته): لا يُحتسب بعد تاريخ الإعفاء — يُستبعد من قوائم اليوم
            if (e.ExemptionDate is { } exDate && DateOnly.FromDateTime(today) > exDate)
                continue;

            if (deviceByEmp.TryGetValue(e.Id, out var recs) && recs.Count > 0)
            {
                var firstIn = recs.First().EventTime;
                var lastRec = recs.Last();
                vm.PresentToday.Add(new DashboardEmpRow(
                    e.Id, e.FullName, groupName, $"أول حضور {firstIn:HH:mm}"));

                // متأخر: يُحتسب كلما وُجد وقت حضور محدّد في الوردية
                if (schedule?.StartTime is { } start)
                {
                    var allowed = start.ToTimeSpan().Add(TimeSpan.FromMinutes(schedule.LateGraceMinutes));
                    if (firstIn.TimeOfDay > allowed)
                    {
                        var mins = (int)Math.Round((firstIn.TimeOfDay - allowed).TotalMinutes);
                        vm.LateToday.Add(new DashboardEmpRow(
                            e.Id, e.FullName, groupName, $"حضر {firstIn:HH:mm} (متأخر {mins} د)"));
                    }
                }

                // ما زال بالداخل: لم يبصم عند/بعد موعد الخروج المضبوط، وآخر بصمة ليست خروجًا
                var checkedOutBySchedule = schedule?.EndTime is { } end &&
                    recs.Any(r => r.EventTime.TimeOfDay >= end.ToTimeSpan());
                if (!checkedOutBySchedule && lastRec.Direction != PunchDirection.CheckOut)
                    vm.StillInside.Add(new DashboardEmpRow(
                        e.Id, e.FullName, groupName, $"آخر حركة {lastRec.EventTime:HH:mm}"));
            }
            // غائب: يوم عمل وليس له بصمة جهاز ولا أي سجل يدوي اليوم
            else if (isWorkingDay && !anyRecordToday.Contains(e.Id))
            {
                vm.AbsentToday.Add(new DashboardEmpRow(e.Id, e.FullName, groupName, null));
            }

            if (!recentIds.Contains(e.Id))
                vm.NoRecord30Days.Add(new DashboardEmpRow(e.Id, e.FullName, groupName, "لا سجل خلال 30 يومًا"));

            if (!recentIds15.Contains(e.Id))
                vm.NoRecord15Days.Add(new DashboardEmpRow(e.Id, e.FullName, groupName, "لا سجل خلال 15 يومًا"));
        }

        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
