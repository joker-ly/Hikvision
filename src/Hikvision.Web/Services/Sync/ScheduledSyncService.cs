using System.Globalization;
using Hikvision.Web.Data;
using Hikvision.Web.Services.Audit;
using Hikvision.Web.Services.TimeZoneSupport;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Services.Sync;

/// <summary>
/// خدمة خلفية تنفّذ المزامنة آليًا يوميًا عند وقت محدد (افتراضي 14:30).
/// إذا كان التطبيق متوقفًا عند الموعد وفُتح بعده، تنفّذ المزامنة الفائتة فورًا.
/// </summary>
public class ScheduledSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ScheduledSyncService> _logger;
    private readonly Hikvision.Web.Services.Setup.DbConnectionStringProvider _dbProvider;

    public ScheduledSyncService(
        IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<ScheduledSyncService> logger,
        Hikvision.Web.Services.Setup.DbConnectionStringProvider dbProvider)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
        _dbProvider = dbProvider;
    }

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

            // فحص كل دقيقتين (يلتقط الموعد بدقة كافية وعند بدء التطبيق)
            try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
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

        var timeStr = _config["Sync:DailyTime"] ?? "14:30";
        if (!TimeOnly.TryParse(timeStr, CultureInfo.InvariantCulture, out var scheduledTime))
            scheduledTime = new TimeOnly(14, 30);

        using var scope = _scopeFactory.CreateScope();
        var clock = scope.ServiceProvider.GetRequiredService<IAppClock>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sync = scope.ServiceProvider.GetRequiredService<IAttendanceSyncService>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        var scheduledToday = today.ToDateTime(scheduledTime);

        var cfg = await db.DeviceConfigs.FirstOrDefaultAsync(ct);
        if (cfg is null) return;

        var lastScheduledDate = cfg.LastScheduledSyncDate is { } d ? DateOnly.FromDateTime(d) : (DateOnly?)null;

        // نفّذ إذا تجاوزنا موعد اليوم ولم تُنفَّذ مزامنة مجدولة اليوم (يشمل حالة الفتح المتأخر)
        if (now >= scheduledToday && (lastScheduledDate is null || lastScheduledDate < today))
        {
            _logger.LogInformation("بدء المزامنة المجدولة لليوم {Today}", today);
            var result = await sync.RunAsync(null, null, null, ct);

            cfg.LastScheduledSyncDate = today.ToDateTime(TimeOnly.MinValue);
            await db.SaveChangesAsync(ct);

            await audit.LogAsync("مزامنة مجدولة آلية",
                $"الفترة {result.FromTime:yyyy-MM-dd HH:mm} - {result.ToTime:yyyy-MM-dd HH:mm}، " +
                $"مُدخل {result.InsertedCount}، نجاح: {result.Success}.");

            _logger.LogInformation("اكتملت المزامنة المجدولة: مُدخل {Inserted}، نجاح {Success}",
                result.InsertedCount, result.Success);
        }
    }
}
