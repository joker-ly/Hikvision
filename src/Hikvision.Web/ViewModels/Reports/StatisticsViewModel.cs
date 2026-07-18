namespace Hikvision.Web.ViewModels.Reports;

/// <summary>نموذج صفحة الإحصائيات والمؤشرات الشاملة.</summary>
public class StatisticsViewModel
{
    public int? GroupId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    public StatsKpis Kpis { get; set; } = new();
    public List<EmployeeStatsRow> Employees { get; set; } = new();
    public List<DailyStatsRow> Daily { get; set; } = new();
    public List<GroupStatsRow> Groups { get; set; } = new();
}

/// <summary>المؤشرات العامة للفترة.</summary>
public class StatsKpis
{
    public int EmployeeCount { get; set; }
    public decimal AvgPresenceRate { get; set; }
    public int TotalAbsentDays { get; set; }
    public int TotalLeaveDays { get; set; }
    public int TotalUnpaidLeaveDays { get; set; }
    public int TotalMissionDays { get; set; }
    public int TotalPermittedExits { get; set; }
    public int TotalLateCount { get; set; }
    public int TotalLateMinutes { get; set; }
}

/// <summary>إحصائيات موظف واحد خلال الفترة.</summary>
public class EmployeeStatsRow
{
    public int EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? FinancialNo { get; set; }
    public string GroupName { get; set; } = string.Empty;

    /// <summary>عضو مجموعة معفاة من البصمة (حضوره محتسب تلقائيًا).</summary>
    public bool IsExempt { get; set; }

    /// <summary>أيام الفترة المحسوبة له (باستثناء ما بعد تاريخ الإعفاء).</summary>
    public int PeriodDays { get; set; }
    public int DaysPresent { get; set; }
    public int DaysAbsent { get; set; }
    public decimal PresenceRate { get; set; }
    public decimal AbsenceRate { get; set; }

    public int DaysLeave { get; set; }
    public int DaysUnpaidLeave { get; set; }
    public int WorkMissionDays { get; set; }
    public int PermittedExitCount { get; set; }
    public int LateCount { get; set; }
    public int TotalLateMinutes { get; set; }
    public decimal TotalWorkedHours { get; set; }
}

/// <summary>تجميعة يوم واحد على مستوى المنشأة.</summary>
public class DailyStatsRow
{
    public DateOnly Date { get; set; }
    public string DayName { get; set; } = string.Empty;
    public bool IsHoliday { get; set; }
    /// <summary>الحاضرون من واقع الاحتساب الفعلي (بدون المعفيين من البصمة).</summary>
    public int Present { get; set; }
    public int Absent { get; set; }
    public int Late { get; set; }
    /// <summary>أعضاء المجموعات المعفاة من البصمة (يُجمعون هنا ولا يدخلون الحاضرين).</summary>
    public int Exempt { get; set; }
    /// <summary>عدد الموظفين المحسوبين في هذا اليوم (يشمل المعفيين).</summary>
    public int Counted { get; set; }
    /// <summary>النسبة من غير المعفيين: حاضرون ÷ (المحسوبون − المعفيون).</summary>
    public decimal PresenceRate { get; set; }
}

/// <summary>ملخص مجموعة واحدة.</summary>
public class GroupStatsRow
{
    public string GroupName { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public decimal AvgPresenceRate { get; set; }
    public int TotalAbsentDays { get; set; }
    public int TotalLateCount { get; set; }
    public int TotalLateMinutes { get; set; }
}
