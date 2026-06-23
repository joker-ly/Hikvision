using System.Security.Claims;
using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Models.Enums;
using Hikvision.Web.Services.TimeZoneSupport;
using Hikvision.Web.ViewModels.ManualAttendance;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Controllers;

public class ManualAttendanceController : Controller
{
    private readonly AppDbContext _db;
    private readonly IAppClock _clock;

    public ManualAttendanceController(AppDbContext db, IAppClock clock)
    {
        _db = db;
        _clock = clock;
    }

    private async Task PopulateEmployees(int? selected = null)
        => ViewBag.Employees = new SelectList(
            await _db.Employees.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(),
            "Id", "FullName", selected);

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateEmployees();
        return View(new ManualEntryViewModel { EventTime = _clock.Now });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ManualEntryViewModel model)
    {
        if (model.ManualType == ManualAttendanceType.None)
            ModelState.AddModelError(nameof(model.ManualType), "اختر نوع السجل اليدوي.");

        if (!ModelState.IsValid)
        {
            await PopulateEmployees(model.EmployeeId);
            return View(model);
        }

        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;

        _db.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = model.EmployeeId,
            EventTime = model.EventTime,
            Source = AttendanceSource.Manual,
            Direction = model.Direction,
            ManualType = model.ManualType,
            Note = model.Note,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = "تمت إضافة السجل اليدوي وتوثيقه.";
        return RedirectToAction("Index", "Attendance", new { source = AttendanceSource.Manual });
    }
}
