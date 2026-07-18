using Hikvision.Web.Models.Enums;

namespace Hikvision.Web.Models.Entities;

/// <summary>صلاحيات مستخدم على نافذة واحدة (عرض/إضافة/تعديل/حذف).</summary>
public class UserPermission
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser? User { get; set; }

    public AppModule Module { get; set; }

    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}
