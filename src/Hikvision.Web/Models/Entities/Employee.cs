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

    public ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();
}
