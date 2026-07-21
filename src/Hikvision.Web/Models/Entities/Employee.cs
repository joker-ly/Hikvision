using System.ComponentModel.DataAnnotations;

namespace Hikvision.Web.Models.Entities;

/// <summary>موظف، مرتبط بمجموعة توظيف وبرقم الموظف على الجهاز.</summary>
public class Employee
{
    public int Id { get; set; }

    public int EmployeeGroupId { get; set; }
    [Display(Name = "المجموعة")]
    public EmployeeGroup? Group { get; set; }

    /// <summary>رقم الموظف على الجهاز (employeeNoString) — مفتاح الربط مع أحداث الجهاز.</summary>
    [Required(ErrorMessage = "رقم الموظف على الجهاز مطلوب"), MaxLength(64)]
    [Display(Name = "رقم الموظف على الجهاز")]
    public string DeviceEmployeeNo { get; set; } = string.Empty;

    /// <summary>رقم الموظف في المنظومة المالية الخارجية (للربط عند تصدير الصرف).</summary>
    [MaxLength(64)]
    [Display(Name = "رقم المنظومة المالية")]
    public string? FinancialNo { get; set; }

    [Required(ErrorMessage = "الاسم مطلوب"), MaxLength(200)]
    [Display(Name = "الاسم الكامل")]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(50)]
    [Display(Name = "رقم الهوية")]
    public string? NationalId { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    /// <summary>الراتب الأساسي الشهري (يُستخدم في تقدير الصرف).</summary>
    [Range(0, double.MaxValue, ErrorMessage = "قيمة غير صحيحة")]
    [Display(Name = "الراتب الأساسي")]
    public decimal? BaseSalary { get; set; }

    [Display(Name = "تاريخ التعيين")]
    [DataType(DataType.Date)]
    public DateOnly? HireDate { get; set; }

    /// <summary>تاريخ الإعفاء: لا يُحتسب أي حضور للموظف بعد هذا التاريخ. فارغ = غير معفى.</summary>
    [Display(Name = "تاريخ الإعفاء")]
    [DataType(DataType.Date)]
    public DateOnly? ExemptionDate { get; set; }

    // ==== بوابة الموظف (تطبيق الهاتف) ====

    /// <summary>الرقم السري لدخول التطبيق (Hash فقط). فارغ = لم يُصدر بعد.</summary>
    public string? PinHash { get; set; }

    /// <summary>عدّاد محاولات الدخول الفاشلة (للقفل المؤقت).</summary>
    public int PinFailedCount { get; set; }

    /// <summary>مقفول عن الدخول حتى هذا الوقت (بعد محاولات فاشلة متكررة).</summary>
    public DateTime? PinLockedUntilUtc { get; set; }

    /// <summary>معرّف جهاز الهاتف الوحيد المسموح له بالدخول. فارغ = لم يُربط جهاز بعد.</summary>
    [MaxLength(64)]
    public string? DeviceId { get; set; }

    /// <summary>وصف/طراز الجهاز المرتبط (للعرض للمدير).</summary>
    [MaxLength(300)]
    public string? DeviceInfo { get; set; }

    public DateTime? DeviceBoundAtUtc { get; set; }

    /// <summary>توكن جلسة التطبيق (SHA256).</summary>
    [MaxLength(100)]
    public string? ApiTokenHash { get; set; }

    public DateTime? ApiTokenExpiresUtc { get; set; }

    public ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();
}
