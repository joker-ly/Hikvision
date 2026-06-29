using Hikvision.Web.Models.Entities;

namespace Hikvision.Web.ViewModels.Home;

public class DashboardViewModel
{
    // إحصاءات عامة
    public int EmployeeCount { get; set; }
    public int GroupCount { get; set; }
    public int AttendanceRecordCount { get; set; }
    public int TodayRecordCount { get; set; }
    public SyncLog? LastSync { get; set; }

    // تقارير اليوم وقوائم
    public List<DashboardEmpRow> PresentToday { get; set; } = new();
    public List<DashboardEmpRow> AbsentToday { get; set; } = new();
    public List<DashboardEmpRow> LateToday { get; set; } = new();
    public List<DashboardEmpRow> StillInside { get; set; } = new();
    public List<DashboardEmpRow> NoRecord15Days { get; set; } = new();
    public List<DashboardEmpRow> NoRecord30Days { get; set; } = new();

    public DateTime Today { get; set; }
}

/// <summary>صف موظف في قوائم لوحة التحكم.</summary>
public record DashboardEmpRow(int Id, string Name, string GroupName, string? Detail);
