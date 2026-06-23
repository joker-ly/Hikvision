using System.ComponentModel.DataAnnotations;
using Hikvision.Web.Models.Enums;

namespace Hikvision.Web.ViewModels.ManualAttendance;

public class ManualEntryViewModel
{
    [Required(ErrorMessage = "اختر الموظف")]
    [Display(Name = "الموظف")]
    public int EmployeeId { get; set; }

    [Required(ErrorMessage = "وقت الحدث مطلوب")]
    [Display(Name = "وقت الحدث")]
    [DataType(DataType.DateTime)]
    public DateTime EventTime { get; set; }

    [Required(ErrorMessage = "اختر نوع السجل")]
    [Display(Name = "نوع السجل")]
    [EnumDataType(typeof(ManualAttendanceType))]
    public ManualAttendanceType ManualType { get; set; } = ManualAttendanceType.WorkMission;

    [Display(Name = "اتجاه البصمة")]
    public PunchDirection Direction { get; set; } = PunchDirection.Undefined;

    [Required(ErrorMessage = "الملاحظة إلزامية لتوثيق السجل اليدوي")]
    [MaxLength(1000)]
    [Display(Name = "ملاحظة / سبب")]
    public string Note { get; set; } = string.Empty;
}
