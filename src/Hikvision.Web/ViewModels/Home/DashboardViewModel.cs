using Hikvision.Web.Models.Entities;

namespace Hikvision.Web.ViewModels.Home;

public class DashboardViewModel
{
    public int EmployeeCount { get; set; }
    public int GroupCount { get; set; }
    public int AttendanceRecordCount { get; set; }
    public int TodayRecordCount { get; set; }
    public SyncLog? LastSync { get; set; }
}
