using System.ComponentModel.DataAnnotations;
using Hikvision.Web.Models.Enums;

namespace Hikvision.Web.Models.Entities;

/// <summary>مجموعة التوظيف (مثل: المدراء، الموظفون).</summary>
public class EmployeeGroup
{
    public int Id { get; set; }

    [Required(ErrorMessage = "اسم المجموعة مطلوب"), MaxLength(150)]
    [Display(Name = "اسم المجموعة")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    /// <summary>
    /// هل المجموعة مقيّدة بأوقات الدوام؟
    /// false: مثل المدراء — بصمة دخول واحدة تكفي لاعتباره حاضرًا.
    /// true: مثل الموظفين — التقيد بحضور وانصراف ضمن مواعيد محددة.
    /// </summary>
    [Display(Name = "مقيّد بمواعيد الدوام")]
    public bool IsTimeBound { get; set; } = true;

    /// <summary>طريقة احتساب الحضور (بالأوقات أو بساعات العمل) — مفتاح قابل للتبديل لكل مجموعة.</summary>
    [Display(Name = "طريقة الاحتساب")]
    public CalculationMode CalculationMode { get; set; } = CalculationMode.ByCheckInOut;

    /// <summary>
    /// معفي من البصمة: عند التفعيل يُحتسب كل أيام الفترة حضورًا كاملًا لأعضاء المجموعة
    /// (لا غياب ولا تأخير)، دون النظر إلى بصمات الجهاز.
    /// </summary>
    [Display(Name = "معفي من البصمة (حضور كامل)")]
    public bool IsFingerprintExempt { get; set; } = false;

    // العلاقات
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public WorkSchedule? Schedule { get; set; }
}
