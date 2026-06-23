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

    /// <summary>نوع السجل اليدوي المطبَّق على هذا اليوم (إن وُجد).</summary>
    public ManualAttendanceType ManualTypeApplied { get; set; } = ManualAttendanceType.None;

    public string? Note { get; set; }
}
