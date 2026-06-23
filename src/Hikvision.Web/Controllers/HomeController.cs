using Hikvision.Web.Data;
using Hikvision.Web.Services.Sync;
using Hikvision.Web.Services.TimeZoneSupport;
using Hikvision.Web.ViewModels.Home;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    public async Task<IActionResult> Index()
    {
        var today = _clock.Now.Date;
        var tomorrow = today.AddDays(1);

        var vm = new DashboardViewModel
        {
            EmployeeCount = await _db.Employees.CountAsync(),
            GroupCount = await _db.EmployeeGroups.CountAsync(),
            AttendanceRecordCount = await _db.AttendanceRecords.CountAsync(),
            TodayRecordCount = await _db.AttendanceRecords
                .CountAsync(r => r.EventTime >= today && r.EventTime < tomorrow),
            LastSync = await _sync.GetLastSyncAsync()
        };
        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
