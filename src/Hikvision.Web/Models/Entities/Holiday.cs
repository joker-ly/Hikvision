using System.ComponentModel.DataAnnotations;

namespace Hikvision.Web.Models.Entities;

/// <summary>إجازة رسمية معمّمة على جميع الموظفين (مثل إجازات الأعياد).</summary>
public class Holiday
{
    public int Id { get; set; }

    [Required(ErrorMessage = "تاريخ البداية مطلوب")]
    [Display(Name = "من تاريخ")]
    [DataType(DataType.Date)]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "تاريخ النهاية مطلوب")]
    [Display(Name = "إلى تاريخ")]
    [DataType(DataType.Date)]
    public DateOnly EndDate { get; set; }

    [Required(ErrorMessage = "الوصف مطلوب"), MaxLength(300)]
    [Display(Name = "الوصف")]
    public string Description { get; set; } = string.Empty;
}
