using Hikvision.Web.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hikvision.Web.Services.Auth;

/// <summary>
/// يفرض صلاحية (نافذة + إجراء) على الإجراء المزيَّن به.
/// عند عدم الامتلاك يُوجَّه المستخدم لصفحة "لا صلاحية".
/// </summary>
public class PermAttribute : TypeFilterAttribute
{
    public PermAttribute(AppModule module, PermAction action) : base(typeof(PermFilter))
    {
        Arguments = new object[] { module, action };
    }
}

public class PermFilter : IAsyncAuthorizationFilter
{
    private readonly AppModule _module;
    private readonly PermAction _action;
    private readonly IPermissionService _perms;

    public PermFilter(AppModule module, PermAction action, IPermissionService perms)
    {
        _module = module;
        _action = action;
        _perms = perms;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // غير المسجّل دخوله يعالجه فلتر المصادقة العام (تحويل لصفحة الدخول)
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            return;

        if (await _perms.HasAsync(_module, _action))
            return;

        context.Result = new RedirectToActionResult("Denied", "Account", null);
    }
}
