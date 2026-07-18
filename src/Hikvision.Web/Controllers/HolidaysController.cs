using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Services.Audit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hikvision.Web.Models.Enums;
using Hikvision.Web.Services.Auth;

namespace Hikvision.Web.Controllers;

public class HolidaysController : Controller
{
    private readonly AppDbContext _db;
    private readonly IAuditLogger _audit;

    public HolidaysController(AppDbContext db, IAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    [Perm(AppModule.Holidays, PermAction.View)]
    public async Task<IActionResult> Index()
        => View(await _db.Holidays.AsNoTracking().OrderByDescending(h => h.StartDate).ToListAsync());

    [HttpGet]
    [Perm(AppModule.Holidays, PermAction.Create)]
    public IActionResult Create()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return View(new Holiday { StartDate = today, EndDate = today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Holidays, PermAction.Create)]
    public async Task<IActionResult> Create([Bind("StartDate,EndDate,Description")] Holiday model)
    {
        if (model.EndDate < model.StartDate)
            ModelState.AddModelError(nameof(Holiday.EndDate), "تاريخ النهاية يجب أن يكون بعد تاريخ البداية.");
        if (!ModelState.IsValid) return View(model);

        _db.Holidays.Add(model);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("إضافة إجازة رسمية",
            $"{model.Description} ({model.StartDate} - {model.EndDate})");
        TempData["Success"] = "تمت إضافة الإجازة الرسمية، وستُحتسب حضورًا للجميع في التقارير.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Perm(AppModule.Holidays, PermAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var h = await _db.Holidays.FindAsync(id);
        if (h is null) return NotFound();
        return View(h);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Holidays, PermAction.Edit)]
    public async Task<IActionResult> Edit(int id, [Bind("Id,StartDate,EndDate,Description")] Holiday model)
    {
        if (id != model.Id) return NotFound();
        if (model.EndDate < model.StartDate)
            ModelState.AddModelError(nameof(Holiday.EndDate), "تاريخ النهاية يجب أن يكون بعد تاريخ البداية.");
        if (!ModelState.IsValid) return View(model);

        var h = await _db.Holidays.FindAsync(id);
        if (h is null) return NotFound();
        h.StartDate = model.StartDate;
        h.EndDate = model.EndDate;
        h.Description = model.Description;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("تعديل إجازة رسمية", $"{h.Description} ({h.StartDate} - {h.EndDate})");
        TempData["Success"] = "تم تحديث الإجازة.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Holidays, PermAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var h = await _db.Holidays.FindAsync(id);
        if (h is null) return NotFound();
        _db.Holidays.Remove(h);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("حذف إجازة رسمية", $"{h.Description} ({h.StartDate} - {h.EndDate})");
        TempData["Success"] = "تم حذف الإجازة.";
        return RedirectToAction(nameof(Index));
    }
}
