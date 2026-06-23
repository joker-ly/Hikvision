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
            var noPerson = new List<NoPersonEvent>();
            // أحداث "بلا شخص" (فتح/إغلاق باب، محاولات فاشلة) لا تخص الحضور وتُتجاهَل.
            // لا نكتب لها ملف سجل إلا عند تفعيله صراحةً للتشخيص.
            var writeNoPersonLog = _config.GetValue("Device:WriteNoPersonLog", false);

            await foreach (var ev in _client.GetEventsAsync(startOff, endOff, ct))
            {
                result.FetchedCount++;

                // أحداث بلا شخص (فتح باب، أحداث نظام، تعرّف فاشل بلا هوية) — ليست عدم تطابق فعلي
                if (string.IsNullOrWhiteSpace(ev.EmployeeNoString))
                {
                    result.NoPersonCount++;
                    if (writeNoPersonLog && noPerson.Count < 50000)
                        noPerson.Add(new NoPersonEvent(
                            ev.Time, ev.Major, ev.Minor, ev.CurrentVerifyMode,
                            ev.CardNo, ev.Name, ev.AttendanceStatus));
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

            // ملف سجل الأحداث بلا شخص — فقط عند تفعيله للتشخيص
            if (writeNoPersonLog && noPerson.Count > 0)
                result.NoPersonLogFile = WriteNoPersonLog(noPerson, result);

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

    /// <summary>حدث بلا شخص، يُلتقط لكتابته في ملف السجل.</summary>
    private record NoPersonEvent(string? Time, int Major, int Minor, string? VerifyMode,
        string? CardNo, string? Name, string? AttendanceStatus);

    /// <summary>يكتب ملف CSV بالأحداث بلا شخص مع ملخّص حسب نوع الحدث، ويعيد اسم الملف.</summary>
    private string? WriteNoPersonLog(List<NoPersonEvent> events, SyncResult result)
    {
        try
        {
            var dir = Path.Combine(_env.ContentRootPath, "logs");
            Directory.CreateDirectory(dir);
            var fileName = $"noperson-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            var path = Path.Combine(dir, fileName);

            var sb = new StringBuilder();
            sb.Append('﻿'); // BOM
            sb.AppendLine($"# مزامنة {result.FromTime:yyyy-MM-dd HH:mm} - {result.ToTime:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"# إجمالي الأحداث بلا شخص: {result.NoPersonCount}");
            sb.AppendLine("#");
            sb.AppendLine("# ملخّص حسب نوع الحدث (major/minor):");
            foreach (var g in events.GroupBy(e => (e.Major, e.Minor)).OrderByDescending(g => g.Count()))
                sb.AppendLine($"# major={g.Key.Major} minor={g.Key.Minor} ({DescribeMinor(g.Key.Minor)}) = {g.Count()}");
            sb.AppendLine();
            sb.AppendLine("الوقت,major,minor,نوع الحدث,نمط التحقق,رقم البطاقة,الاسم,حالة الحضور");
            foreach (var e in events)
                sb.AppendLine(string.Join(",",
                    Csv(e.Time), e.Major, e.Minor, Csv(DescribeMinor(e.Minor)),
                    Csv(e.VerifyMode), Csv(e.CardNo), Csv(e.Name), Csv(e.AttendanceStatus)));

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "تعذّر كتابة ملف سجل الأحداث بلا شخص");
            return null;
        }
    }

    private static string Csv(string? v)
    {
        v ??= string.Empty;
        return v.Contains(',') || v.Contains('"') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }

    /// <summary>وصف مختصر لأكثر رموز minor شيوعًا في أحداث التحكم بالدخول.</summary>
    private static string DescribeMinor(int minor) => minor switch
    {
        1 => "تحقق بطاقة ناجح",
        2 => "بطاقة غير موجودة",
        38 => "تعرّف وجه ناجح",
        75 => "محاولة فاشلة",
        21 => "فتح الباب",
        22 => "إغلاق الباب",
        23 => "بقاء الباب مفتوحًا",
        199 => "فتح بالزر",
        _ => "غير معروف"
    };

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
