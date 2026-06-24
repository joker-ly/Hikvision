using System.ComponentModel.DataAnnotations;

namespace Hikvision.Web.ViewModels.Setup;

public class SetupViewModel
{
    [Required(ErrorMessage = "اسم الخادم مطلوب")]
    [Display(Name = "اسم خادم SQL Server")]
    public string Server { get; set; } = "localhost";

    [Required(ErrorMessage = "اسم قاعدة البيانات مطلوب")]
    [Display(Name = "اسم قاعدة البيانات")]
    public string Database { get; set; } = "HikvisionAttendance";

    [Display(Name = "استخدام مصادقة ويندوز")]
    public bool UseWindowsAuth { get; set; } = true;

    [Display(Name = "اسم المستخدم")]
    public string? Username { get; set; }

    [Display(Name = "كلمة المرور")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }
}
