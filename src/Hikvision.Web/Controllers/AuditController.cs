using Hikvision.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hikvision.Web.Models.Enums;
using Hikvision.Web.Services.Auth;

namespace Hikvision.Web.Controllers;

public class AuditController : Controller
{
    private readonly AppDbContext _db;
    public AuditController(AppDbContext db) => _db = db;

    [Perm(AppModule.Audit, PermAction.View)]
    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 100;
        if (page < 1) page = 1;

        var total = await _db.AuditLogs.CountAsync();
        var logs = await _db.AuditLogs
            .OrderByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsNoTracking().ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        return View(logs);
    }
}
