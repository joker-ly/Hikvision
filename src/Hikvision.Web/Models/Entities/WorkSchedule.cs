using System.ComponentModel.DataAnnotations;

namespace Hikvision.Web.Models.Entities;

/// <summary>وردية/مواعيد الدوام لمجموعة توظيف (واحدة لكل مجموعة في الإصدار الحالي).</summary>
public class WorkSchedule
{
    public int Id { get; set; }

    public int EmployeeGroupId { get; set; }
    public EmployeeGroup? Group { get; set; }

    [MaxLength(150)]
    [Display(Name = "اسم الوردية")]
    public string Name { get; set; } = "الوردية الافتراضية";

    /// <summary>وقت بدء الدوام. قد يكون فارغًا للمجموعات غير المقيّدة بوقت (المدراء).</summary>
    [Display(Name = "وقت الحضور")]
    [DataType(DataType.Time)]
    public TimeOnly? StartTime { get; set; }

    /// <summary>وقت انتهاء الدوام. قد يكون فارغًا للمجموعات غير المقيّدة بوقت.</summary>
    [Display(Name = "وقت الانصراف")]
    [DataType(DataType.Time)]
    public TimeOnly? EndTime { get; set; }

    /// <summary>دقائق السماح قبل احتساب التأخير.</summary>
    [Range(0, 240, ErrorMessage = "قيمة غير صحيحة")]
    [Display(Name = "سماح التأخير (دقائق)")]
    public int LateGraceMinutes { get; set; } = 0;

    /// <summary>عدد ساعات العمل المطلوبة يوميًا (يُستخدم عند الاحتساب بساعات العمل).</summary>
    [Range(0, 24, ErrorMessage = "قيمة غير صحيحة")]
    [Display(Name = "ساعات العمل المطلوبة يوميًا")]
    public decimal? RequiredDailyHours { get; set; }

    /// <summary>
    /// أيام العمل كقناع بِتّي (Sunday=bit0 .. Saturday=bit6).
    /// الافتراضي: الأحد–الخميس = 0b0011111 = 31.
    /// </summary>
    [Display(Name = "أيام العمل")]
    public int WorkDaysMask { get; set; } = 0b0011111;

    /// <summary>هل اليوم المعطى يوم عمل وفق القناع؟</summary>
    public bool IsWorkingDay(DayOfWeek day) => (WorkDaysMask & (1 << (int)day)) != 0;
}
