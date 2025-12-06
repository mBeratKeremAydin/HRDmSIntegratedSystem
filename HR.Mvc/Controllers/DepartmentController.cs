using HRDms.Data.Context;
using HRDms.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

//[Authorize] // En azından login olma şartı
public class DepartmentController : Controller
{
    private readonly AppDbContext _context;

    public DepartmentController(AppDbContext context)
    {
        _context = context;
    }

    // ADMIN + HR + DepManager görebilsin
    //[Authorize(Roles = "Admin,HR,DepManager")]
    public IActionResult Index()
    {
        var deps = _context.Departments
            .Include(d => d.Location)
            .Include(d => d.Manager)
            .ToList();

        return View(deps);
    }

    // SADECE Admin + HR departman EKLEYEBİLSİN
    //[Authorize(Roles = "Admin,HR")]
    [HttpGet]
    public IActionResult Create()
    {
        ViewBag.Locations = new SelectList(_context.Locations, "LocationId", "LocationName");
        ViewBag.Managers = new SelectList(_context.Employees, "EmployeeId", "FirstName");
        return View();
    }

    //[Authorize(Roles = "Admin,HR")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Department model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Locations = new SelectList(_context.Locations, "LocationId", "LocationName", model.LocationId);
            ViewBag.Managers = new SelectList(_context.Employees, "EmployeeId", "FirstName", model.ManagerId);
            return View(model);
        }

        _context.Departments.Add(model);
        _context.SaveChanges();
        return RedirectToAction("Index");
    }

    // Admin + HR + DepManager düzenleyebilsin
    //[Authorize(Roles = "Admin,HR,DepManager")]
    [HttpGet]
    public IActionResult Edit(int id)
    {
        var dep = _context.Departments.Find(id);
        if (dep == null) return NotFound();

        ViewBag.Locations = new SelectList(_context.Locations, "LocationId", "LocationName", dep.LocationId);
        ViewBag.Managers = new SelectList(_context.Employees, "EmployeeId", "FirstName", dep.ManagerId);
        return View(dep);
    }

    //[Authorize(Roles = "Admin,HR,DepManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(Department model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Locations = new SelectList(_context.Locations, "LocationId", "LocationName", model.LocationId);
            ViewBag.Managers = new SelectList(_context.Employees, "EmployeeId", "FirstName", model.ManagerId);
            return View(model);
        }

        _context.Departments.Update(model);
        _context.SaveChanges();
        return RedirectToAction("Index");
    }

    // SİLMEYİ sadece Admin'e bırak
    //[Authorize(Roles = "Admin")]
    public IActionResult Delete(int id)
    {
        var dep = _context.Departments.Find(id);
        if (dep == null) return NotFound();

        _context.Departments.Remove(dep);
        _context.SaveChanges();
        return RedirectToAction("Index");
    }
}
