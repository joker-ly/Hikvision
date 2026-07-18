using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Hikvision.Web.Services.Audit;
using Hikvision.Web.Services.Hikvision;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Hikvision.Web.Models.Enums;
using Hikvision.Web.Services.Auth;

namespace Hikvision.Web.Controllers;

public class EmployeesController : Controller
{
    private readonly AppDbContext _db;
    private readonly IHikvisionIsapiClient _client;
    private readonly IAuditLogger _audit;

    public EmployeesController(AppDbContext db, IHikvisionIsapiClient client, IAuditLogger audit)
    {
        _db = db;
        _client = client;
        _audit = audit;
    }

    private async Task PopulateGroups(int? selected = null)
        => ViewBag.Groups = new SelectList(await _db.EmployeeGroups.AsNoTracking().ToListAsync(), "Id", "Name", selected);

    [Perm(AppModule.Employees, PermAction.View)]
    public async Task<IActionResult> Index(string? sort, string? search)
    {
        var query = _db.Employees.Include(e => e.Group).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e =>
                (e.FinancialNo != null && e.FinancialNo.Contains(term)) ||
                e.FullName.Contains(term) ||
                e.DeviceEmployeeNo.Contains(term));
        }

        var list = await query.ToListAsync();

        // ترتيب الرقم المالي رقميًا (لا نصيًا) حتى يأتي 9 قبل 90 وليس بعد 89
        static long FinKey(string? s) => long.TryParse(s, out var n) ? n : long.MaxValue;
        var employees = sort switch
        {
            "name_desc" => list.OrderByDescending(e => e.FullName).ToList(),
            "financial" => list.OrderBy(e => FinKey(e.FinancialNo)).ThenBy(e => e.FullName).ToList(),
            "financial_desc" => list.OrderByDescending(e => FinKey(e.FinancialNo)).ThenBy(e => e.FullName).ToList(),
            "group" => list.OrderBy(e => e.Group?.Name).ThenBy(e => e.FullName).ToList(),
            "group_desc" => list.OrderByDescending(e => e.Group?.Name).ThenBy(e => e.FullName).ToList(),
            _ => list.OrderBy(e => e.FullName).ToList()
        };
        ViewBag.Sort = sort ?? "name";
        ViewBag.Search = search;
        await PopulateGroups();
        return View(employees);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Employees, PermAction.Create)]
    public async Task<IActionResult> ImportFromDevice(int groupId)
    {
        if (!await _db.EmployeeGroups.AnyAsync(g => g.Id == groupId))
        {
            TempData["Error"] = "اختر مجموعة صحيحة لاستيراد الموظفين إليها.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var existingNos = await _db.Employees
                .Select(e => e.DeviceEmployeeNo).ToListAsync();
            var existing = new HashSet<string>(existingNos);

            int imported = 0, skipped = 0;
            var toAdd = new List<Employee>();

            await foreach (var user in _client.GetUsersAsync())
            {
                if (existing.Contains(user.EmployeeNo))
                {
                    skipped++;
                    continue;
                }
                existing.Add(user.EmployeeNo); // تفادي التكرار داخل نفس الدفعة
                toAdd.Add(new Employee
                {
                    DeviceEmployeeNo = user.EmployeeNo,
                    FullName = string.IsNullOrWhiteSpace(user.Name) ? $"موظف {user.EmployeeNo}" : user.Name!,
                    EmployeeGroupId = groupId,
                    IsActive = true
                });
                imported++;
            }

            if (toAdd.Count > 0)
            {
                _db.Employees.AddRange(toAdd);
                await _db.SaveChangesAsync();
            }

            await _audit.LogAsync("استيراد موظفين من الجهاز", $"مستورد {imported}، موجود مسبقًا {skipped}، إلى المجموعة #{groupId}.");
            TempData["Success"] = $"اكتمل الاستيراد من الجهاز: مستورد {imported}، موجود مسبقًا {skipped}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"فشل الاستيراد من الجهاز: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Perm(AppModule.Employees, PermAction.Create)]
    public async Task<IActionResult> Create()
    {
        await PopulateGroups();
        return View(new Employee());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Employees, PermAction.Create)]
    public async Task<IActionResult> Create([Bind("EmployeeGroupId,DeviceEmployeeNo,FinancialNo,FullName,NationalId,IsActive,BaseSalary,HireDate")] Employee model)
    {
        if (await _db.Employees.AnyAsync(e => e.DeviceEmployeeNo == model.DeviceEmployeeNo))
            ModelState.AddModelError(nameof(Employee.DeviceEmployeeNo), "رقم الموظف على الجهاز مستخدم بالفعل.");

        if (!ModelState.IsValid)
        {
            await PopulateGroups(model.EmployeeGroupId);
            return View(model);
        }

        _db.Employees.Add(model);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("إضافة موظف", $"{model.FullName} (رقم جهاز {model.DeviceEmployeeNo}).");
        TempData["Success"] = "تمت إضافة الموظف.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Perm(AppModule.Employees, PermAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var emp = await _db.Employees.FindAsync(id);
        if (emp is null) return NotFound();
        await PopulateGroups(emp.EmployeeGroupId);
        return View(emp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Employees, PermAction.Edit)]
    public async Task<IActionResult> Edit(int id, [Bind("Id,EmployeeGroupId,DeviceEmployeeNo,FinancialNo,FullName,NationalId,IsActive,BaseSalary,HireDate,ExemptionDate")] Employee model)
    {
        if (id != model.Id) return NotFound();

        if (await _db.Employees.AnyAsync(e => e.DeviceEmployeeNo == model.DeviceEmployeeNo && e.Id != id))
            ModelState.AddModelError(nameof(Employee.DeviceEmployeeNo), "رقم الموظف على الجهاز مستخدم بالفعل.");

        if (!ModelState.IsValid)
        {
            await PopulateGroups(model.EmployeeGroupId);
            return View(model);
        }

        var emp = await _db.Employees.FindAsync(id);
        if (emp is null) return NotFound();

        emp.EmployeeGroupId = model.EmployeeGroupId;
        emp.DeviceEmployeeNo = model.DeviceEmployeeNo;
        emp.FinancialNo = model.FinancialNo;
        emp.FullName = model.FullName;
        emp.NationalId = model.NationalId;
        emp.IsActive = model.IsActive;
        emp.BaseSalary = model.BaseSalary;
        emp.HireDate = model.HireDate;
        emp.ExemptionDate = model.ExemptionDate;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("تعديل موظف", $"{emp.FullName} (رقم جهاز {emp.DeviceEmployeeNo}).");
        TempData["Success"] = "تم تحديث بيانات الموظف.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Employees, PermAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var emp = await _db.Employees.FindAsync(id);
        if (emp is null) return NotFound();
        _db.Employees.Remove(emp);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("حذف موظف", $"{emp.FullName} (رقم جهاز {emp.DeviceEmployeeNo}).");
        TempData["Success"] = "تم حذف الموظف.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Perm(AppModule.Employees, PermAction.Delete)]
    public async Task<IActionResult> DeleteAll()
    {
        // تُحذف سجلات الحضور المرتبطة أولًا ثم جميع الموظفين
        await _db.AttendanceRecords.ExecuteDeleteAsync();
        var count = await _db.Employees.ExecuteDeleteAsync();
        await _audit.LogAsync("حذف جميع الموظفين", $"تم حذف {count} موظفًا وكل سجلات حضورهم.");
        TempData["Success"] = $"تم حذف جميع الموظفين ({count}) وسجلات حضورهم. يمكنك الآن إعادة الاستيراد بالتوزيع الصحيح.";
        return RedirectToAction(nameof(Index));
    }
}
