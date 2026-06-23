namespace Hikvision.Web.Services.TimeZoneSupport;

/// <summary>
/// ساعة التطبيق التي تتعامل مع المنطقة الزمنية المُعدّة.
/// كل أوقات الأحداث تُخزَّن وتُعرض بهذه المنطقة.
/// </summary>
public interface IAppClock
{
    /// <summary>الوقت الحالي في المنطقة الزمنية المُعدّة (DateTime محلي).</summary>
    DateTime Now { get; }

    TimeZoneInfo TimeZone { get; }

    /// <summary>تحويل وقت محلي (في المنطقة المُعدّة) إلى DateTimeOffset.</summary>
    DateTimeOffset ToOffset(DateTime local);

    /// <summary>تحويل DateTimeOffset (بأي منطقة) إلى وقت محلي بالمنطقة المُعدّة.</summary>
    DateTime ToLocal(DateTimeOffset value);
}

public class AppClock : IAppClock
{
    public TimeZoneInfo TimeZone { get; }

    public AppClock(IConfiguration config)
    {
        var id = config["Localization:TimeZone"] ?? "UTC";
        TimeZone = ResolveTimeZone(id);
    }

    public DateTime Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZone).DateTime;

    public DateTimeOffset ToOffset(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = TimeZone.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset);
    }

    public DateTime ToLocal(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, TimeZone).DateTime;

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }
}
