using System.ComponentModel.DataAnnotations;

namespace Hikvision.Web.Models.Enums;

/// <summary>
/// نوع سجل الحضور اليدوي. يُحدَّد فقط عندما يكون المصدر Manual.
/// </summary>
public enum ManualAttendanceType
{
    /// <summary>لا ينطبق (السجل من الجهاز).</summary>
    [Display(Name = "لا ينطبق")]
    None = 0,

    /// <summary>مهمة عمل خارج المقر.</summary>
    [Display(Name = "مهمة عمل")]
    WorkMission = 1,

    /// <summary>إجازة موظف.</summary>
    [Display(Name = "إجازة")]
    Leave = 2,

    /// <summary>خروج بإذن.</summary>
    [Display(Name = "خروج بإذن")]
    PermittedExit = 3,

    /// <summary>إنجاز عمل.</summary>
    [Display(Name = "إنجاز عمل")]
    TaskDone = 4
}
