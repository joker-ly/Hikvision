using Hikvision.Web.Models.Entities;

namespace Hikvision.Web.Services.Sync;

public class SyncResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int FetchedCount { get; set; }
    public int InsertedCount { get; set; }
    public int SkippedDuplicateCount { get; set; }
    public int UnmatchedEmployeeCount { get; set; }
    /// <summary>أحداث بلا شخص (فتح باب/أحداث نظام) — ليست عدم تطابق فعلي.</summary>
    public int NoPersonCount { get; set; }
    /// <summary>عيّنة من أرقام الأجهزة التي لم تطابق أي موظف (للمراجعة).</summary>
    public List<string> UnmatchedNumbers { get; set; } = new();
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
}

public interface IAttendanceSyncService
{
    /// <summary>
    /// تنفيذ مزامنة الحضور من الجهاز.
    /// إذا كان from فارغًا تُستخدم آخر مزامنة أو فترة الرجوع الافتراضية.
    /// </summary>
    Task<SyncResult> RunAsync(DateTime? from, DateTime? to, int? userId, CancellationToken ct = default);

    Task<SyncLog?> GetLastSyncAsync(CancellationToken ct = default);
}
