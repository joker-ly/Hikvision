using System.ComponentModel.DataAnnotations;

namespace Hikvision.Web.Models.Enums;

/// <summary>نوافذ النظام التي تُضبط عليها الصلاحيات.</summary>
public enum AppModule
{
    [Display(Name = "لوحة التحكم")] Dashboard = 1,
    [Display(Name = "المجموعات")] Groups = 2,
    [Display(Name = "مواعيد الدوام")] Schedules = 3,
    [Display(Name = "الموظفون")] Employees = 4,
    [Display(Name = "الإجازات الرسمية")] Holidays = 5,
    [Display(Name = "سجلات الحضور")] Attendance = 6,
    [Display(Name = "الإدخال اليدوي")] ManualAttendance = 7,
    [Display(Name = "المزامنة مع الجهاز")] Sync = 8,
    [Display(Name = "تقرير المرتبات")] Payroll = 9,
    [Display(Name = "الإحصائيات")] Statistics = 10,
    [Display(Name = "سجل العمليات")] Audit = 11,
    [Display(Name = "الإعدادات")] Settings = 12,
    [Display(Name = "المستخدمون")] Users = 13
}

/// <summary>نوع الإجراء على النافذة.</summary>
public enum PermAction
{
    View = 1,
    Create = 2,
    Edit = 3,
    Delete = 4
}
