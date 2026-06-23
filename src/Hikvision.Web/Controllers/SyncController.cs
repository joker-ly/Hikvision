using System.Security.Claims;
using Hikvision.Web.Services.Sync;
using Hikvision.Web.Services.TimeZoneSupport;
using Microsoft.AspNetCore.Mvc;

namespace Hikvision.Web.Controllers;

public class SyncController : Controller
{
    private readonly IAttendanceSyncService _sync;
    private readonly IAppClock _clock;
    private readonly IHostEnvironment _env;

    public SyncController(IAttendanceSyncService sync, IAppClock clock, IHostEnvironment env)
    {
        _sync = sync;
        _clock = clock;
        _env = env;
    }

    private string LogsDir => Path.Combine(_env.ContentRootPath, "logs");

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.LastSync = await _sync.GetLastSyncAsync();
        ViewBag.DefaultFrom = (ViewBag.LastSync as Hikvision.Web.Models.Entities.SyncLog)?.ToTime
            ?? _clock.Now.AddDays(-7);
        ViewBag.DefaultTo = _clock.Now;

        // قائمة ملفات السجل (الأحدث أولًا): أرقام غير مسجّلة + أحداث بلا شخص
        ViewBag.LogFiles = Directory.Exists(LogsDir)
            ? new DirectoryInfo(LogsDir).GetFiles("*.csv")
                .Where(f => f.Name.StartsWith("unmatched-") || f.Name.StartsWith("noperson-"))
                .OrderByDescending(f => f.CreationTimeUtc)
                .Take(40)
                .Select(f => f.Name)
                .ToList()
            : new List<string>();

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
                $"مكرر {result.SkippedDuplicateCount}، أرقام غير مسجّلة {result.UnmatchedEmployeeCount}. " +
                $"تم تجاهل {result.NoPersonCount} حدثًا غير متعلق بالحضور (فتح/إغلاق باب ومحاولات فاشلة).";
            if (result.UnmatchedLogFile is not null || result.NoPersonLogFile is not null)
                msg += " تم حفظ ملف(ات) سجل قابلة للتنزيل من أسفل الصفحة.";
            TempData["Success"] = msg;
        }
        else
        {
            TempData["Error"] = $"فشلت المزامنة: {result.ErrorMessage}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult DownloadLog(string name)
    {
        // حماية من اجتياز المسارات: نسمح فقط باسم ملف بسيط ضمن مجلد logs
        var safeName = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(safeName) || !safeName.EndsWith(".csv") ||
            !(safeName.StartsWith("unmatched-") || safeName.StartsWith("noperson-")))
            return NotFound();

        var path = Path.Combine(LogsDir, safeName);
        if (!System.IO.File.Exists(path)) return NotFound();

        var bytes = System.IO.File.ReadAllBytes(path);
        return File(bytes, "text/csv", safeName);
    }
}
