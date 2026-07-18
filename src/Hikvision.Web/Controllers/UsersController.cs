using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Models.Enums;
using Hikvision.Web.Services.Audit;
using Hikvision.Web.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Controllers;

/// <summary>إدارة مستخدمي النظام وصلاحياتهم المفصّلة لكل نافذة.</summary>
public class UsersController : Controller
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<AppUser> _hasher;
    private readonly IAuditLogger _audit;

    public UsersController(AppDbContext db, PasswordHasher<AppUser> hasher, IAuditLogger audit)
    {
        _db = db;
        _hasher = hasher;
        _audit = audit;
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private static string ModuleName(AppModule m) =>
        m.GetType().GetField(m.ToString())?
            .GetCustomAttributes(typeof(DisplayAttribute), false)
            .Cast<DisplayAttribute>().FirstOrDefault()?.Name ?? m.ToString();

    [Perm(AppModule.Users, PermAction.View)]
    public async Task<IActionResult> Index()
    {
        var users = await _db.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync();
        return View(users);
    }

    [HttpGet]
    [Perm(AppModule.Users, PermAction.Create)]
    public IActionResult Create() => View(new ViewModels.Users.UserFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Users, PermAction.Create)]
    public async Task<IActionResult> Create(ViewModels.Users.UserFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "كلمة المرور مطلوبة عند الإنشاء.");
        if (await _db.Users.AnyAsync(u => u.Username == model.Username))
            ModelState.AddModelError(nameof(model.Username), "اسم المستخدم مستخدم بالفعل.");
        if (!ModelState.IsValid) return View(model);

        var user = new AppUser
        {
            Username = model.Username.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? model.Username : model.DisplayName.Trim(),
            IsAdmin = model.IsAdmin,
            IsActive = model.IsActive
        };
        user.PasswordHash = _hasher.HashPassword(user, model.Password!);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("إضافة مستخدم", $"{user.Username} (مدير: {(user.IsAdmin ? "نعم" : "لا")}).");

        TempData["Success"] = user.IsAdmin
            ? "تمت إضافة المستخدم كمدير نظام (يملك كل الصلاحيات)."
            : "تمت إضافة المستخدم. حدّد صلاحياته الآن.";
        return user.IsAdmin
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Permissions), new { id = user.Id });
    }

    [HttpGet]
    [Perm(AppModule.Users, PermAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();
        return View(new ViewModels.Users.UserFormViewModel
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            IsAdmin = user.IsAdmin,
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Users, PermAction.Edit)]
    public async Task<IActionResult> Edit(ViewModels.Users.UserFormViewModel model)
    {
        var user = await _db.Users.FindAsync(model.Id);
        if (user is null) return NotFound();

        if (await _db.Users.AnyAsync(u => u.Username == model.Username && u.Id != model.Id))
            ModelState.AddModelError(nameof(model.Username), "اسم المستخدم مستخدم بالفعل.");

        // حماية: لا يمكن تعطيل أو سحب إدارة آخر مدير نظام
        var isLastAdmin = user.IsAdmin &&
            !await _db.Users.AnyAsync(u => u.IsAdmin && u.IsActive && u.Id != user.Id);
        if (isLastAdmin && (!model.IsAdmin || !model.IsActive))
            ModelState.AddModelError(string.Empty, "لا يمكن تعطيل أو سحب صلاحية آخر مدير نظام.");

        if (!ModelState.IsValid) return View(model);

        user.Username = model.Username.Trim();
        user.DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? model.Username : model.DisplayName.Trim();
        user.IsAdmin = model.IsAdmin;
        user.IsActive = model.IsActive;
        if (!string.IsNullOrWhiteSpace(model.Password))
            user.PasswordHash = _hasher.HashPassword(user, model.Password);

        await _db.SaveChangesAsync();
        await _audit.LogAsync("تعديل مستخدم", $"{user.Username} (مدير: {(user.IsAdmin ? "نعم" : "لا")}، نشط: {(user.IsActive ? "نعم" : "لا")}).");
        TempData["Success"] = "تم تحديث المستخدم.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Perm(AppModule.Users, PermAction.Edit)]
    public async Task<IActionResult> Permissions(int id)
    {
        var user = await _db.Users.Include(u => u.Permissions)
            .AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        var vm = new ViewModels.Users.PermissionsViewModel
        {
            UserId = user.Id,
            Username = user.Username,
            IsAdmin = user.IsAdmin
        };
        foreach (var m in Enum.GetValues<AppModule>())
        {
            var p = user.Permissions.FirstOrDefault(x => x.Module == m);
            vm.Rows.Add(new ViewModels.Users.PermRow
            {
                Module = m,
                ModuleName = ModuleName(m),
                CanView = p?.CanView ?? false,
                CanCreate = p?.CanCreate ?? false,
                CanEdit = p?.CanEdit ?? false,
                CanDelete = p?.CanDelete ?? false
            });
        }
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Users, PermAction.Edit)]
    public async Task<IActionResult> Permissions(ViewModels.Users.PermissionsViewModel model)
    {
        var user = await _db.Users.Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Id == model.UserId);
        if (user is null) return NotFound();

        // استبدال كامل لصفوف الصلاحيات (تُحفظ فقط النوافذ التي بها صلاحية واحدة على الأقل)
        _db.UserPermissions.RemoveRange(user.Permissions);
        foreach (var row in model.Rows)
        {
            if (!(row.CanView || row.CanCreate || row.CanEdit || row.CanDelete)) continue;
            _db.UserPermissions.Add(new UserPermission
            {
                UserId = user.Id,
                Module = row.Module,
                CanView = row.CanView,
                CanCreate = row.CanCreate,
                CanEdit = row.CanEdit,
                CanDelete = row.CanDelete
            });
        }
        await _db.SaveChangesAsync();
        await _audit.LogAsync("تعديل صلاحيات", $"المستخدم {user.Username}.");
        TempData["Success"] = $"تم حفظ صلاحيات {user.Username}. تسري فورًا.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Users, PermAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (user.Id == CurrentUserId)
        {
            TempData["Error"] = "لا يمكنك حذف حسابك الحالي.";
            return RedirectToAction(nameof(Index));
        }
        if (user.IsAdmin && !await _db.Users.AnyAsync(u => u.IsAdmin && u.IsActive && u.Id != user.Id))
        {
            TempData["Error"] = "لا يمكن حذف آخر مدير نظام.";
            return RedirectToAction(nameof(Index));
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("حذف مستخدم", user.Username);
        TempData["Success"] = "تم حذف المستخدم.";
        return RedirectToAction(nameof(Index));
    }
}
