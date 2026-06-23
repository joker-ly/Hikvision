using System.ComponentModel.DataAnnotations;
using Hikvision.Web.Models.Enums;

namespace Hikvision.Web.ViewModels.ManualAttendance;

public class ManualEntryViewModel
{
    [Required(ErrorMessage = "اختر الموظف")]
    [Display(Name = "الموظف")]
    public int EmployeeId { get; set; }

    [Required(ErrorMessage = "اختر نوع السجل")]
    [Display(Name = "نوع السجل")]
    [EnumDataType(typeof(ManualAttendanceType))]
    public ManualAttendanceType ManualType { get; set; } = ManualAttendanceType.Leave;

    [Required(ErrorMessage = "تاريخ البداية مطلوب")]
    [Display(Name = "من تاريخ")]
    [DataType(DataType.Date)]
    public DateOnly FromDate { get; set; }

    [Required(ErrorMessage = "تاريخ النهاية مطلوب")]
    [Display(Name = "إلى تاريخ")]
    [DataType(DataType.Date)]
    public DateOnly ToDate { get; set; }

    /// <summary>وقت الدخول (افتراضي 09:00). يُستخدم للإجازات والمهام لتوليد بصمة دخول آلية.</summary>
    [Display(Name = "وقت الدخول")]
    [DataType(DataType.Time)]
    public TimeOnly StartTime { get; set; } = new(9, 0);

    /// <summary>وقت الخروج (افتراضي 14:00). يُستخدم لتوليد بصمة خروج آلية، أو وقت إذن الخروج المبكر.</summary>
    [Display(Name = "وقت الخروج")]
    [DataType(DataType.Time)]
    public TimeOnly EndTime { get; set; } = new(14, 0);

    [Required(ErrorMessage = "الملاحظة إلزامية لتوثيق السجل اليدوي")]
    [MaxLength(1000)]
    [Display(Name = "ملاحظة / سبب")]
    public string Note { get; set; } = string.Empty;

    /// <summary>توليد السجلات لأيام العمل فقط (حسب وردية المجموعة).</summary>
    [Display(Name = "أيام العمل فقط")]
    public bool WorkingDaysOnly { get; set; } = true;
}
