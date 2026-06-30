using Hikvision.Web.Models.Enums;

namespace Hikvision.Web.Services.Calculation;

/// <summary>نتيجة احتساب حضور موظف ليوم واحد.</summary>
public class DailyAttendanceResult
{
    public DateOnly Date { get; set; }
    public bool IsWorkingDay { get; set; }

    public DateTime? FirstIn { get; set; }
    public DateTime? LastOut { get; set; }
    public decimal WorkedHours { get; set; }

    public bool IsPresent { get; set; }
    public bool IsAbsent { get; set; }
    public bool IsLate { get; set; }
    public int LateMinutes { get; set; }

    /// <summary>إجازة رسمية معمّمة (تُحتسب حضورًا للجميع).</summary>
    public bool IsHoliday { get; set; }

    /// <summary>مغادرة مبكرة (قبل موعد الخروج) — تُحتسب غيابًا ما لم يوجد إذن خروج.</summary>
    public bool IsEarlyLeave { get; set; }

    /// <summary>إجازة بدون مرتب — لا تُحتسب حضورًا وتُخصم من الصرف.</summary>
    public bool IsUnpaidLeave { get; set; }

    /// <summary>وُجد إذن خروج مبكر لهذا اليوم.</summary>
    public bool HasPermittedExit { get; set; }

    /// <summary>نوع السجل اليدوي المطبَّق على هذا اليوم (إن وُجد).</summary>
    public ManualAttendanceType ManualTypeApplied { get; set; } = ManualAttendanceType.None;

    public string? Note { get; set; }
}
