using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Services.Audit;
using Hikvision.Web.Services.Hikvision;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Controllers;

public class SettingsController : Controller
{
    private readonly AppDbContext _db;
    private readonly IHikvisionIsapiClient _client;
    private readonly IAuditLogger _audit;

    public SettingsController(AppDbContext db, IHikvisionIsapiClient client, IAuditLogger audit)
    {
        _db = db;
        _client = client;
        _audit = audit;
    }

    private async Task<DeviceConfig> GetOrCreateAsync()
    {
        var cfg = await _db.DeviceConfigs.FirstOrDefaultAsync();
        if (cfg is null)
        {
            cfg = new DeviceConfig();
            _db.DeviceConfigs.Add(cfg);
            await _db.SaveChangesAsync();
        }
        return cfg;
    }

    [HttpGet]
    public async Task<IActionResult> Device() => View(await GetOrCreateAsync());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Device([Bind("Id,Host,Port,UseHttps,Username,Password")] DeviceConfig model)
    {
        if (!ModelState.IsValid) return View(model);

        var cfg = await GetOrCreateAsync();
        cfg.Host = model.Host;
        cfg.Port = model.Port;
        cfg.UseHttps = model.UseHttps;
        cfg.Username = model.Username;
        cfg.Password = model.Password;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("تعديل إعدادات الجهاز", $"{cfg.Host}:{cfg.Port}، مستخدم {cfg.Username}.");

        TempData["Success"] = "تم حفظ إعدادات الجهاز.";
        return RedirectToAction(nameof(Device));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection()
    {
        var (ok, message) = await _client.TestConnectionAsync();
        if (ok) TempData["Success"] = message;
        else TempData["Error"] = message;
        return RedirectToAction(nameof(Device));
    }
}
