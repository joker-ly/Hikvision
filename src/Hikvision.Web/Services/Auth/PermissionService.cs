using System.Security.Claims;
using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Services.Auth;

public interface IPermissionService
{
    /// <summary>هل يملك المستخدم الحالي الصلاحية المطلوبة؟ (المدير يملك الكل)</summary>
    Task<bool> HasAsync(AppModule module, PermAction action);

    Task<bool> CanViewAsync(AppModule module);

    /// <summary>هل المستخدم الحالي مدير نظام؟</summary>
    Task<bool> IsAdminAsync();
}

/// <summary>
/// يقرأ صلاحيات المستخدم الحالي من قاعدة البيانات (مع تخزين ضمن الطلب الواحد)
/// فتسري تعديلات الصلاحيات فورًا دون الحاجة لإعادة تسجيل الدخول.
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    private bool _userLoaded;
    private AppUser? _user;
    private List<UserPermission>? _perms;

    public PermissionService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    private async Task<AppUser?> GetUserAsync()
    {
        if (_userLoaded) return _user;
        _userLoaded = true;

        var idStr = _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idStr, out var id))
            _user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
        return _user;
    }

    public async Task<bool> IsAdminAsync() => (await GetUserAsync())?.IsAdmin == true;

    public async Task<bool> HasAsync(AppModule module, PermAction action)
    {
        var user = await GetUserAsync();
        if (user is null) return false;
        if (user.IsAdmin) return true;

        _perms ??= await _db.UserPermissions.AsNoTracking()
            .Where(p => p.UserId == user.Id).ToListAsync();

        var p = _perms.FirstOrDefault(x => x.Module == module);
        if (p is null) return false;

        return action switch
        {
            PermAction.View => p.CanView,
            PermAction.Create => p.CanCreate,
            PermAction.Edit => p.CanEdit,
            PermAction.Delete => p.CanDelete,
            _ => false
        };
    }

    public Task<bool> CanViewAsync(AppModule module) => HasAsync(module, PermAction.View);
}
