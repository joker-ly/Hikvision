using System.Globalization;
using System.Text;
using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Models.Enums;
using Hikvision.Web.Services.Hikvision;
using Hikvision.Web.Services.TimeZoneSupport;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Services.Sync;

public class AttendanceSyncService : IAttendanceSyncService
{
    private readonly AppDbContext _db;
    private readonly IHikvisionIsapiClient _client;
    private readonly IConfiguration _config;
    private readonly IAppClock _clock;
    private readonly IHostEnvironment _env;
    private readonly ILogger<AttendanceSyncService> _logger;

    public AttendanceSyncService(
        AppDbContext db, IHikvisionIsapiClient client, IConfiguration config,
        IAppClock clock, IHostEnvironment env, ILogger<AttendanceSyncService> logger)
    {
        _db = db;
        _client = client;
        _config = config;
        _clock = clock;
        _env = env;
        _logger = logger;
    }

    public Task<SyncLog?> GetLastSyncAsync(CancellationToken ct = default) =>
        _db.SyncLogs.AsNoTracking().OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);

    public async Task<SyncResult> RunAsync(DateTime? from, DateTime? to, int? userId, CancellationToken ct = default)
    {
        var cfg = await _db.DeviceConfigs.FirstOrDefaultAsync(ct);
        var nowLocal = _clock.Now;

        var lookbackDays = _config.GetValue("Device:DefaultLookbackDays", 7);
        var fromLocal = from ?? cfg?.LastSyncTime ?? nowLocal.AddDays(-lookbackDays);
        var toLocal = to ?? nowLocal;

        var log = new SyncLog
        {
            StartedAtUtc = DateTime.UtcNow,
            FromTime = fromLocal,
            ToTime = toLocal
        };
        _db.SyncLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        var result = new SyncResult { FromTime = fromLocal, ToTime = toLocal };

        try
        {
            // خرائط بحث سريعة
            var employeeByDeviceNo = await _db.Employees
                .AsNoTracking()
                .Select(e => new { e.Id, e.DeviceEmployeeNo })
                .ToDictionaryAsync(e => e.DeviceEmployeeNo, e => e.Id, ct);

            // تحميل مفاتيح السجلات الموجودة دفعة واحدة لمنع التكرار في الذاكرة
            var existing = new HashSet<(int, DateTime, PunchDirection)>(
                (await _db.AttendanceRecords
                    .Where(r => r.Source == AttendanceSource.Device &&
                                r.EventTime >= fromLocal && r.EventTime <= toLocal)
                    .Select(r => new { r.EmployeeId, r.EventTime, r.Direction })
                    .ToListAsync(ct))
                .Select(x => (x.EmployeeId, x.EventTime, x.Direction)));

            var batch = new HashSet<(int, DateTime, PunchDirection)>();

            var startOff = _clock.ToOffset(fromLocal);
            var endOff = _clock.ToOffset(toLocal);

            var toAdd = new List<AttendanceRecord>();
            var unmatchedNumbers = new Dictionary<string, int>();

            await foreach (var ev in _client.GetEventsAsync(startOff, endOff, ct))
            {
                result.FetchedCount++;

                // أحداث بلا شخص (فتح باب، أحداث نظام، تعرّف فاشل بلا هوية) — ليست عدم تطابق فعلي
                if (string.IsNullOrWhiteSpace(ev.EmployeeNoString))
                {
                    result.NoPersonCount++;
                    continue;
                }

                // رقم موجود على الجهاز لكنه غير مسجّل لدينا كموظف
                if (!employeeByDeviceNo.TryGetValue(ev.EmployeeNoString, out var empId))
                {
                    result.UnmatchedEmployeeCount++;
                    unmatchedNumbers[ev.EmployeeNoString] =
                        unmatchedNumbers.GetValueOrDefault(ev.EmployeeNoString) + 1;
                    continue;
                }

                if (!TryParseEventTime(ev.Time, out var eventLocal))
                    continue;

                var direction = MapDirection(ev.AttendanceStatus);
                var key = (empId, eventLocal, direction);

                // منع التكرار في الذاكرة (موجود مسبقًا أو ضمن الدفعة الحالية)
                if (existing.Contains(key) || !batch.Add(key))
                {
                    result.SkippedDuplicateCount++;
                    continue;
                }

                toAdd.Add(new AttendanceRecord
                {
                    EmployeeId = empId,
                    EventTime = eventLocal,
                    Source = AttendanceSource.Device,
                    Direction = direction,
                    ManualType = ManualAttendanceType.None,
                    VerifyMode = ev.CurrentVerifyMode,
                    PictureUrl = ev.PictureURL,
                    SerialNo = ev.SerialNo?.ToString(CultureInfo.InvariantCulture),
                    CreatedAtUtc = DateTime.UtcNow
                });
                result.InsertedCount++;
            }

            if (toAdd.Count > 0)
                _db.AttendanceRecords.AddRange(toAdd);

            if (cfg is not null)
                cfg.LastSyncTime = toLocal;

            // عيّنة للعرض + كتابة ملف سجل كامل بالأرقام غير المسجّلة
            result.UnmatchedNumbers = unmatchedNumbers.Keys.OrderBy(x => x).Take(100).ToList();
            if (unmatchedNumbers.Count > 0)
            {
                _logger.LogWarning(
                    "مزامنة: {Count} رقم جهاز غير مسجّل كموظف. الأرقام: {Numbers}",
                    unmatchedNumbers.Count, string.Join(", ", result.UnmatchedNumbers));

                result.UnmatchedLogFile = WriteUnmatchedLog(unmatchedNumbers, result);
            }

            log.Success = true;
            log.FetchedCount = result.FetchedCount;
            log.InsertedCount = result.InsertedCount;
            log.SkippedDuplicateCount = result.SkippedDuplicateCount;
            log.UnmatchedEmployeeCount = result.UnmatchedEmployeeCount;
            log.FinishedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            result.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشلت عملية المزامنة");
            log.Success = false;
            log.ErrorMessage = ex.Message;
            log.FinishedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>يكتب ملف CSV بالأرقام غير المسجّلة وعدد أحداث كل رقم، ويعيد اسم الملف.</summary>
    private string? WriteUnmatchedLog(Dictionary<string, int> unmatched, SyncResult result)
    {
        try
        {
            var dir = Path.Combine(_env.ContentRootPath, "logs");
            Directory.CreateDirectory(dir);
            var fileName = $"unmatched-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            var path = Path.Combine(dir, fileName);

            var sb = new StringBuilder();
            sb.Append('﻿'); // BOM لإظهار العربية في Excel
            sb.AppendLine($"# مزامنة {result.FromTime:yyyy-MM-dd HH:mm} - {result.ToTime:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"# إجمالي الأرقام غير المسجّلة: {unmatched.Count} | إجمالي الأحداث غير المسجّلة: {result.UnmatchedEmployeeCount}");
            sb.AppendLine("رقم الجهاز,عدد الأحداث");
            foreach (var kv in unmatched.OrderByDescending(x => x.Value).ThenBy(x => x.Key))
                sb.AppendLine($"{kv.Key},{kv.Value}");

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "تعذّر كتابة ملف سجل الأرقام غير المسجّلة");
            return null;
        }
    }

    private bool TryParseEventTime(string? raw, out DateTime localTime)
    {
        localTime = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var dto))
            return false;
        localTime = _clock.ToLocal(dto);
        return true;
    }

    /// <summary>تحويل attendanceStatus القادم من الجهاز إلى اتجاه البصمة.</summary>
    public static PunchDirection MapDirection(string? status) => (status ?? "").ToLowerInvariant() switch
    {
        "checkin" => PunchDirection.CheckIn,
        "checkout" => PunchDirection.CheckOut,
        "breakout" => PunchDirection.BreakOut,
        "breakin" => PunchDirection.BreakIn,
        _ => PunchDirection.Undefined
    };
}
