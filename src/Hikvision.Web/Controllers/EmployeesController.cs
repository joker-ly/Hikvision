using Hikvision.Web.Data;
using Hikvision.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Hikvision.Web.Controllers;

public class EmployeesController : Controller
{
    private readonly AppDbContext _db;
    public EmployeesController(AppDbContext db) => _db = db;

    private async Task PopulateGroups(int? selected = null)
        => ViewBag.Groups = new SelectList(await _db.EmployeeGroups.AsNoTracking().ToListAsync(), "Id", "Name", selected);

    public async Task<IActionResult> Index()
    {
        var employees = await _db.Employees
            .Include(e => e.Group)
            .AsNoTracking().OrderBy(e => e.FullName).ToListAsync();
        return View(employees);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateGroups();
        return View(new Employee());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("EmployeeGroupId,DeviceEmployeeNo,FullName,NationalId,IsActive,BaseSalary,HireDate")] Employee model)
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
        TempData["Success"] = "تمت إضافة الموظف.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var emp = await _db.Employees.FindAsync(id);
        if (emp is null) return NotFound();
        await PopulateGroups(emp.EmployeeGroupId);
        return View(emp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,EmployeeGroupId,DeviceEmployeeNo,FullName,NationalId,IsActive,BaseSalary,HireDate")] Employee model)
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
        emp.FullName = model.FullName;
        emp.NationalId = model.NationalId;
        emp.IsActive = model.IsActive;
        emp.BaseSalary = model.BaseSalary;
        emp.HireDate = model.HireDate;

        await _db.SaveChangesAsync();
        TempData["Success"] = "تم تحديث بيانات الموظف.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var emp = await _db.Employees.FindAsync(id);
        if (emp is null) return NotFound();
        _db.Employees.Remove(emp);
        await _db.SaveChangesAsync();
        TempData["Success"] = "تم حذف الموظف.";
        return RedirectToAction(nameof(Index));
    }
}
