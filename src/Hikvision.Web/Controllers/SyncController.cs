using System.Security.Claims;
using Hikvision.Web.Services.Sync;
using Hikvision.Web.Services.TimeZoneSupport;
using Microsoft.AspNetCore.Mvc;

namespace Hikvision.Web.Controllers;

public class SyncController : Controller
{
    private readonly IAttendanceSyncService _sync;
    private readonly IAppClock _clock;

    public SyncController(IAttendanceSyncService sync, IAppClock clock)
    {
        _sync = sync;
        _clock = clock;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.LastSync = await _sync.GetLastSyncAsync();
        ViewBag.DefaultFrom = (ViewBag.LastSync as Hikvision.Web.Models.Entities.SyncLog)?.ToTime
            ?? _clock.Now.AddDays(-7);
        ViewBag.DefaultTo = _clock.Now;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(DateTime? from, DateTime? to)
    {
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;
        var result = await _sync.RunAsync(from, to, userId);

        if (result.Success)
        {
            var msg =
                $"اكتملت المزامنة: مسحوب {result.FetchedCount}، مُدخل {result.InsertedCount}، " +
                $"مكرر {result.SkippedDuplicateCount}، بلا شخص {result.NoPersonCount}، " +
                $"أرقام غير مسجّلة {result.UnmatchedEmployeeCount}.";
            if (result.UnmatchedNumbers.Count > 0)
                msg += " الأرقام غير المسجّلة: " + string.Join("، ", result.UnmatchedNumbers);
            TempData["Success"] = msg;
        }
        else
        {
            TempData["Error"] = $"فشلت المزامنة: {result.ErrorMessage}";
        }
        return RedirectToAction(nameof(Index));
    }
}
