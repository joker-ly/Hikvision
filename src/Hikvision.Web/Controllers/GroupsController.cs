using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hikvision.Web.Models.Enums;
using Hikvision.Web.Services.Auth;

namespace Hikvision.Web.Controllers;

public class GroupsController : Controller
{
    private readonly AppDbContext _db;
    private readonly Hikvision.Web.Services.Audit.IAuditLogger _audit;
    public GroupsController(AppDbContext db, Hikvision.Web.Services.Audit.IAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    [Perm(AppModule.Groups, PermAction.View)]
    public async Task<IActionResult> Index()
    {
        var groups = await _db.EmployeeGroups
            .Include(g => g.Schedule)
            .Include(g => g.Employees)
            .AsNoTracking().ToListAsync();
        return View(groups);
    }

    [HttpGet]
    [Perm(AppModule.Groups, PermAction.Create)]
    public IActionResult Create() => View(new EmployeeGroup());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Groups, PermAction.Create)]
    public async Task<IActionResult> Create([Bind("Name,Description,IsTimeBound,CalculationMode,IsFingerprintExempt")] EmployeeGroup model)
    {
        if (!ModelState.IsValid) return View(model);

        // إنشاء وردية افتراضية مرتبطة بالمجموعة
        model.Schedule = new WorkSchedule { Name = "وردية " + model.Name };
        if (model.IsTimeBound)
        {
            model.Schedule.StartTime = new TimeOnly(8, 0);
            model.Schedule.EndTime = new TimeOnly(16, 0);
            model.Schedule.RequiredDailyHours = 8m;
        }

        _db.EmployeeGroups.Add(model);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("إضافة مجموعة", model.Name);
        TempData["Success"] = "تمت إضافة المجموعة بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Perm(AppModule.Groups, PermAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var group = await _db.EmployeeGroups.FindAsync(id);
        if (group is null) return NotFound();
        return View(group);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Groups, PermAction.Edit)]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,IsTimeBound,CalculationMode,IsFingerprintExempt")] EmployeeGroup model)
    {
        if (id != model.Id) return NotFound();
        if (!ModelState.IsValid) return View(model);

        var group = await _db.EmployeeGroups.FindAsync(id);
        if (group is null) return NotFound();

        group.Name = model.Name;
        group.Description = model.Description;
        group.IsTimeBound = model.IsTimeBound;
        group.CalculationMode = model.CalculationMode;
        group.IsFingerprintExempt = model.IsFingerprintExempt;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("تعديل مجموعة", group.Name);
        TempData["Success"] = "تم تحديث المجموعة.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Groups, PermAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var group = await _db.EmployeeGroups.Include(g => g.Employees).FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound();
        if (group.Employees.Any())
        {
            TempData["Error"] = "لا يمكن حذف مجموعة تحتوي على موظفين.";
            return RedirectToAction(nameof(Index));
        }
        _db.EmployeeGroups.Remove(group);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("حذف مجموعة", group.Name);
        TempData["Success"] = "تم حذف المجموعة.";
        return RedirectToAction(nameof(Index));
    }
}
