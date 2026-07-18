using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hikvision.Web.Models.Enums;
using Hikvision.Web.Services.Auth;

namespace Hikvision.Web.Controllers;

public class SchedulesController : Controller
{
    private readonly AppDbContext _db;
    public SchedulesController(AppDbContext db) => _db = db;

    [Perm(AppModule.Schedules, PermAction.View)]
    public async Task<IActionResult> Index()
    {
        var groups = await _db.EmployeeGroups
            .Include(g => g.Schedule)
            .AsNoTracking().ToListAsync();
        return View(groups);
    }

    [HttpGet]
    [Perm(AppModule.Schedules, PermAction.Edit)]
    public async Task<IActionResult> Edit(int groupId)
    {
        var group = await _db.EmployeeGroups
            .Include(g => g.Schedule)
            .FirstOrDefaultAsync(g => g.Id == groupId);
        if (group is null) return NotFound();

        // إنشاء وردية إن لم تكن موجودة
        var schedule = group.Schedule ?? new WorkSchedule { EmployeeGroupId = group.Id, Name = "وردية " + group.Name };
        ViewBag.GroupName = group.Name;
        ViewBag.IsTimeBound = group.IsTimeBound;
        return View(schedule);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Schedules, PermAction.Edit)]
    public async Task<IActionResult> Edit(WorkSchedule model)
    {
        var group = await _db.EmployeeGroups
            .Include(g => g.Schedule)
            .FirstOrDefaultAsync(g => g.Id == model.EmployeeGroupId);
        if (group is null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.GroupName = group.Name;
            ViewBag.IsTimeBound = group.IsTimeBound;
            return View(model);
        }

        if (group.Schedule is null)
        {
            model.Id = 0;
            _db.WorkSchedules.Add(model);
        }
        else
        {
            var s = group.Schedule;
            s.Name = model.Name;
            s.StartTime = model.StartTime;
            s.EndTime = model.EndTime;
            s.LateGraceMinutes = model.LateGraceMinutes;
            s.CheckoutGraceMinutes = model.CheckoutGraceMinutes;
            s.RequiredDailyHours = model.RequiredDailyHours;
            s.WorkDaysMask = model.WorkDaysMask;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "تم حفظ مواعيد الدوام.";
        return RedirectToAction(nameof(Index));
    }
}
