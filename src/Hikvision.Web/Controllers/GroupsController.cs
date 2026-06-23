using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Controllers;

public class GroupsController : Controller
{
    private readonly AppDbContext _db;
    public GroupsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var groups = await _db.EmployeeGroups
            .Include(g => g.Schedule)
            .Include(g => g.Employees)
            .AsNoTracking().ToListAsync();
        return View(groups);
    }

    [HttpGet]
    public IActionResult Create() => View(new EmployeeGroup());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Description,IsTimeBound,CalculationMode")] EmployeeGroup model)
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
        TempData["Success"] = "تمت إضافة المجموعة بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var group = await _db.EmployeeGroups.FindAsync(id);
        if (group is null) return NotFound();
        return View(group);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,IsTimeBound,CalculationMode")] EmployeeGroup model)
    {
        if (id != model.Id) return NotFound();
        if (!ModelState.IsValid) return View(model);

        var group = await _db.EmployeeGroups.FindAsync(id);
        if (group is null) return NotFound();

        group.Name = model.Name;
        group.Description = model.Description;
        group.IsTimeBound = model.IsTimeBound;
        group.CalculationMode = model.CalculationMode;

        await _db.SaveChangesAsync();
        TempData["Success"] = "تم تحديث المجموعة.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
        TempData["Success"] = "تم حذف المجموعة.";
        return RedirectToAction(nameof(Index));
    }
}
