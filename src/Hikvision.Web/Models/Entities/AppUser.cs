using System.ComponentModel.DataAnnotations;

namespace Hikvision.Web.Models.Entities;

/// <summary>مستخدم النظام (مدير واحد في الإصدار الحالي).</summary>
public class AppUser
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(150)]
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>مدير النظام: يملك كل الصلاحيات ويدير المستخدمين.</summary>
    public bool IsAdmin { get; set; }

    public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();
}
