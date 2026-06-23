using System.ComponentModel.DataAnnotations;

namespace Hikvision.Web.ViewModels.Reports;

public class PayrollReportViewModel
{
    [Display(Name = "المجموعة")]
    public int? GroupId { get; set; }

    [Display(Name = "من تاريخ")]
    [DataType(DataType.Date)]
    public DateOnly From { get; set; }

    [Display(Name = "إلى تاريخ")]
    [DataType(DataType.Date)]
    public DateOnly To { get; set; }

    public List<PayrollRow> Rows { get; set; } = new();
}

public class PayrollRow
{
    public int EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;

    public int DaysPresent { get; set; }
    public int DaysAbsent { get; set; }
    public int DaysLeave { get; set; }
    public int WorkMissionDays { get; set; }
    public int EarlyExitPermissionCount { get; set; }
    public int HolidayDays { get; set; }
    public int LateCount { get; set; }
    public int TotalLateMinutes { get; set; }
    public decimal TotalWorkedHours { get; set; }

    public decimal? BaseSalary { get; set; }
    public decimal? EstimatedPay { get; set; }
}
