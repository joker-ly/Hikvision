using Hikvision.Web.Services.Hikvision.Dtos;

namespace Hikvision.Web.Services.Hikvision;

/// <summary>عميل الاتصال بجهاز Hikvision عبر بروتوكول ISAPI.</summary>
public interface IHikvisionIsapiClient
{
    /// <summary>جلب أحداث الحضور ضمن المدى الزمني، مع ترقيم الصفحات تلقائيًا.</summary>
    IAsyncEnumerable<AcsEventInfo> GetEventsAsync(
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);

    /// <summary>اختبار الاتصال بالجهاز والمصادقة. يعيد رسالة وصفية ونجاح/فشل.</summary>
    Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken ct = default);
}
