using Hikvision.Web.Data;
using Hikvision.Web.Services.Setup;
using Hikvision.Web.ViewModels.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Hikvision.Web.Controllers;

/// <summary>
/// معالج الإعداد الأول: يُعرض عندما تتعذّر تهيئة قاعدة البيانات. يتيح إدخال اسم الخادم
/// واختبار الاتصال ثم الحفظ، وبعدها يهيّئ القاعدة (إنشاء/زرع) ويتابع التطبيق طبيعيًا.
/// </summary>
[AllowAnonymous]
public class SetupController : Controller
{
    private readonly DbConnectionStringProvider _provider;
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _config;
    private readonly ILogger<SetupController> _logger;

    public SetupController(DbConnectionStringProvider provider, IServiceProvider sp,
        IConfiguration config, ILogger<SetupController> logger)
    {
        _provider = provider;
        _sp = sp;
        _config = config;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (_provider.IsReady) return Redirect("/");
        return View(new SetupViewModel());
    }

    /// <summary>اختبار الاتصال بالخادم (يتصل بقاعدة master فلا يشترط وجود قاعدة التطبيق بعد).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(SetupViewModel model)
    {
        var (ok, message) = await TryConnectAsync(model);
        return Json(new { ok, message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SetupViewModel model)
    {
        if (!ModelState.IsValid) return View(nameof(Index), model);

        var (ok, message) = await TryConnectAsync(model);
        if (!ok)
        {
            TempData["Error"] = $"فشل الاتصال بالخادم: {message}";
            return View(nameof(Index), model);
        }

        _provider.Save(BuildConnectionString(model, forTest: false));

        try
        {
            // إنشاء قاعدة البيانات والجداول وزرع البيانات الأولية بالسلسلة الجديدة.
            await DbSeeder.SeedAsync(_sp, _config);
            _provider.MarkReady();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل تهيئة قاعدة البيانات بعد حفظ الإعداد");
            TempData["Error"] = $"تم الاتصال بالخادم لكن فشلت تهيئة قاعدة البيانات: {ex.Message}";
            return View(nameof(Index), model);
        }

        TempData["Success"] = "تم إعداد قاعدة البيانات بنجاح. سجّل الدخول للمتابعة.";
        return Redirect("/Account/Login");
    }

    private static async Task<(bool ok, string message)> TryConnectAsync(SetupViewModel model)
    {
        try
        {
            await using var conn = new SqlConnection(BuildConnectionString(model, forTest: true));
            await conn.OpenAsync();
            return (true, "نجح الاتصال بخادم SQL Server.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// يبني سلسلة الاتصال. عند الاختبار يستهدف قاعدة master للتحقق من الخادم فقط؛
    /// وعند الحفظ يستهدف قاعدة التطبيق (تُنشأ تلقائيًا عند التهيئة).
    /// </summary>
    private static string BuildConnectionString(SetupViewModel model, bool forTest)
    {
        var b = new SqlConnectionStringBuilder
        {
            DataSource = model.Server,
            InitialCatalog = forTest
                ? "master"
                : (string.IsNullOrWhiteSpace(model.Database) ? "HikvisionAttendance" : model.Database),
            TrustServerCertificate = true,
            MultipleActiveResultSets = true,
            ConnectTimeout = 15
        };

        if (model.UseWindowsAuth)
        {
            b.IntegratedSecurity = true;
        }
        else
        {
            b.UserID = model.Username ?? string.Empty;
            b.Password = model.Password ?? string.Empty;
        }

        return b.ConnectionString;
    }
}
