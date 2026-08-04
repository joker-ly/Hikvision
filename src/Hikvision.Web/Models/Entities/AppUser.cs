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

    // ==== جلسة تطبيق الهاتف (واجهة الأدمن) ====

    /// <summary>توكن جلسة التطبيق (SHA256).</summary>
    [MaxLength(100)]
    public string? ApiTokenHash { get; set; }

    public DateTime? ApiTokenExpiresUtc { get; set; }

    /// <summary>عدّاد محاولات الدخول الفاشلة من التطبيق.</summary>
    public int LoginFailedCount { get; set; }

    /// <summary>مقفول عن الدخول من التطبيق حتى هذا الوقت.</summary>
    public DateTime? LockedUntilUtc { get; set; }

    public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();
}
