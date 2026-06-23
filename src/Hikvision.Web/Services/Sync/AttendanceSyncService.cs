using System.Globalization;
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
    private readonly ILogger<AttendanceSyncService> _logger;

    public AttendanceSyncService(
        AppDbContext db, IHikvisionIsapiClient client, IConfiguration config,
        IAppClock clock, ILogger<AttendanceSyncService> logger)
    {
        _db = db;
        _client = client;
        _config = config;
        _clock = clock;
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
                .ToDictionaryAsync(e => e.DeviceEmployeeNo, e => e, ct);

            var startOff = _clock.ToOffset(fromLocal);
            var endOff = _clock.ToOffset(toLocal);

            var toAdd = new List<AttendanceRecord>();

            await foreach (var ev in _client.GetEventsAsync(startOff, endOff, ct))
            {
                result.FetchedCount++;

                if (string.IsNullOrWhiteSpace(ev.EmployeeNoString) ||
                    !employeeByDeviceNo.TryGetValue(ev.EmployeeNoString, out var emp))
                {
                    result.UnmatchedEmployeeCount++;
                    continue;
                }

                if (!TryParseEventTime(ev.Time, out var eventLocal))
                    continue;

                var direction = MapDirection(ev.AttendanceStatus);

                // منع التكرار: فحص قاعدة البيانات + الدفعة الحالية
                var existsInDb = await _db.AttendanceRecords.AnyAsync(r =>
                    r.EmployeeId == emp.Id &&
                    r.EventTime == eventLocal &&
                    r.Direction == direction &&
                    r.Source == AttendanceSource.Device, ct);

                var existsInBatch = toAdd.Any(r =>
                    r.EmployeeId == emp.Id && r.EventTime == eventLocal &&
                    r.Direction == direction && r.Source == AttendanceSource.Device);

                if (existsInDb || existsInBatch)
                {
                    result.SkippedDuplicateCount++;
                    continue;
                }

                toAdd.Add(new AttendanceRecord
                {
                    EmployeeId = emp.Id,
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
