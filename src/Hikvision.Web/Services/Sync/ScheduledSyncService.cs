using System.Globalization;
using Hikvision.Web.Data;
using Hikvision.Web.Services.Audit;
using Hikvision.Web.Services.Setup;
using Hikvision.Web.Services.TimeZoneSupport;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Services.Sync;

/// <summary>
/// خدمة خلفية تنفّذ المزامنة آليًا كل فترة (افتراضي 15 دقيقة) ضمن نافذة الدوام
/// (افتراضي 08:00 حتى 15:00)، منسّقةً على فواصل الساعة (8:00، 8:15، 8:30...).
/// وبعد نهاية النافذة تُنفَّذ "مزامنة إغلاق" واحدة تلتقط بصمات الخروج المتأخرة.
/// </summary>
public class ScheduledSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ScheduledSyncService> _logger;
    private readonly DbConnectionStringProvider _dbProvider;

    /// <summary>مفتاح آخر فترة زمنية نُفّذت مزامنتها (رقم اليوم × 1000 + رقم الفترة).</summary>
    private long? _lastSlotKey;

    public ScheduledSyncService(
        IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<ScheduledSyncService> logger,
        DbConnectionStringProvider dbProvider)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
        _dbProvider = dbProvider;
    }

    internal static TimeOnly ParseTime(string? value, TimeOnly fallback) =>
        TimeOnly.TryParse(value ?? string.Empty, CultureInfo.InvariantCulture, out var t) ? t : fallback;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRunAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في خدمة المزامنة المجدولة");
            }

            // فحص كل دقيقة ليصيب فواصل الربع ساعة بدقة كافية
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task CheckAndRunAsync(CancellationToken ct)
    {
        // لا تعمل قبل اكتمال إعداد قاعدة البيانات (معالج الإعداد الأول).
        if (!_dbProvider.IsReady)
            return;

        if (!_config.GetValue("Sync:Enabled", true))
            return;

        var interval = Math.Clamp(_config.GetValue("Sync:IntervalMinutes", 15), 5, 120);
        var windowStart = ParseTime(_config["Sync:WindowStart"], new TimeOnly(8, 0));
        var windowEnd = ParseTime(_config["Sync:WindowEnd"], new TimeOnly(15, 0));

        using var scope = _scopeFactory.CreateScope();
        var clock = scope.ServiceProvider.GetRequiredService<IAppClock>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sync = scope.ServiceProvider.GetRequiredService<IAttendanceSyncService>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

        var now = clock.Now;
        var t = TimeOnly.FromDateTime(now);
        var today = DateOnly.FromDateTime(now);

        // 1) داخل النافذة: مزامنة كل فترة، منسّقة على فواصل الساعة (8:00، 8:15...)
        if (t >= windowStart && t <= windowEnd)
        {
            var slot = (long)((t - windowStart).TotalMinutes / interval);
            var key = today.DayNumber * 1000L + slot;
            if (_lastSlotKey == key) return;
            _lastSlotKey = key;

            _logger.LogInformation("مزامنة دورية (كل {Interval} د) — الفترة {Slot} ليوم {Today}",
                interval, slot, today);
            var result = await sync.RunAsync(null, null, null, ct);
            _logger.LogInformation("اكتملت المزامنة الدورية: مُدخل {Inserted}، نجاح {Success}",
                result.InsertedCount, result.Success);
            return;
        }

        // 2) بعد نهاية النافذة: مزامنة إغلاق واحدة يوميًا (تلتقط الخروج عند/بعد النهاية)
        if (t > windowEnd)
        {
            var cfg = await db.DeviceConfigs.FirstOrDefaultAsync(ct);
            if (cfg is null) return;

            var lastClosing = cfg.LastScheduledSyncDate is { } d ? DateOnly.FromDateTime(d) : (DateOnly?)null;
            if (lastClosing is not null && lastClosing >= today) return;

            _logger.LogInformation("مزامنة الإغلاق اليومية بعد نهاية النافذة ({End})", windowEnd);
            var result = await sync.RunAsync(null, null, null, ct);

            cfg.LastScheduledSyncDate = today.ToDateTime(TimeOnly.MinValue);
            await db.SaveChangesAsync(ct);

            await audit.LogAsync("مزامنة الإغلاق اليومية",
                $"الفترة {result.FromTime:yyyy-MM-dd HH:mm} - {result.ToTime:yyyy-MM-dd HH:mm}، " +
                $"مُدخل {result.InsertedCount}، نجاح: {result.Success}.");
        }
    }
}
