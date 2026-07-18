using System.ComponentModel.DataAnnotations;
using Hikvision.Web.Models.Enums;

namespace Hikvision.Web.ViewModels.Users;

public class UserFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "اسم المستخدم مطلوب"), MaxLength(100)]
    [Display(Name = "اسم المستخدم")]
    public string Username { get; set; } = string.Empty;

    [MaxLength(150)]
    [Display(Name = "الاسم الظاهر")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>مطلوبة عند الإنشاء؛ عند التعديل تُترك فارغة للإبقاء على الحالية.</summary>
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور")]
    public string? Password { get; set; }

    [Display(Name = "مدير نظام (كل الصلاحيات)")]
    public bool IsAdmin { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

public class PermissionsViewModel
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public List<PermRow> Rows { get; set; } = new();
}

public class PermRow
{
    public AppModule Module { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}
