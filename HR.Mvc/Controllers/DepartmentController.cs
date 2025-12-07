using HRDms.Data.Context;
using HRDms.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

public class DepartmentController : Controller
{
    private readonly AppDbContext _context;

    public DepartmentController(AppDbContext context)
    {
        _context = context;
    }

    // DepartmentManager için ana sayfa
    public IActionResult Index()
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        var employeeId = HttpContext.Session.GetInt32("EmployeeId");

        if (userRole == null || employeeId == null)
        {
            return RedirectToAction("Index", "Login");
        }

        List<Department> departments;

        // DepartmentManager ise sadece yöneticisi olduğu departmanları göster
        if (userRole == "DepartmentManager" || userRole == "DepManager")
        {
            departments = _context.Departments
                .Include(d => d.Location)
                .Include(d => d.Manager)
                .Include(d => d.Employees)
                .Where(d => d.ManagerId == employeeId.Value)
                .ToList();
        }
        // HR/Admin ise tüm departmanları göster
        else if (userRole == "HR" || userRole == "Admin")
        {
            departments = _context.Departments
                .Include(d => d.Location)
                .Include(d => d.Manager)
                .Include(d => d.Employees)
                .ToList();
        }
        else
        {
            TempData["ErrorMessage"] = "Bu sayfayı görüntüleme yetkiniz yok!";
            return RedirectToAction("Index", "Employee");
        }

        return View(departments);
    }

    // HR + DepartmentManager düzenleyebilir
    [HttpGet]
    public IActionResult Edit(int id)
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        var employeeId = HttpContext.Session.GetInt32("EmployeeId");

        var dep = _context.Departments.Find(id);
        if (dep == null) return NotFound();

        // DepartmentManager ise sadece kendi departmanını düzenleyebilir
        if ((userRole == "DepartmentManager" || userRole == "DepManager") && dep.ManagerId != employeeId)
        {
            TempData["ErrorMessage"] = "Sadece yöneticisi olduğunuz departmanı düzenleyebilirsiniz!";
            return RedirectToAction("Index");
        }

        ViewBag.Locations = new SelectList(_context.Locations, "LocationId", "LocationName", dep.LocationId);
        
        // Sadece HR/Admin yönetici seçebilir
        var isHRorAdmin = userRole == "HR" || userRole == "Admin";
        ViewBag.CanChangeManager = isHRorAdmin;
        
        if (isHRorAdmin)
        {
            ViewBag.Managers = new SelectList(_context.Employees.Select(e => new
            {
                e.EmployeeId,
                FullName = e.FirstName + " " + e.LastName
            }), "EmployeeId", "FullName", dep.ManagerId);
        }

        return View(dep);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(Department model)
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        var isHRorAdmin = userRole == "HR" || userRole == "Admin";

        if (!ModelState.IsValid)
        {
            ViewBag.Locations = new SelectList(_context.Locations, "LocationId", "LocationName", model.LocationId);
            ViewBag.CanChangeManager = isHRorAdmin;
            
            if (isHRorAdmin)
            {
                ViewBag.Managers = new SelectList(_context.Employees.Select(e => new
                {
                    e.EmployeeId,
                    FullName = e.FirstName + " " + e.LastName
                }), "EmployeeId", "FullName", model.ManagerId);
            }
            
            return View(model);
        }

        // DB'deki departmanı çek
        var department = _context.Departments
            .FirstOrDefault(d => d.DepartmentId == model.DepartmentId);

        if (department == null)
            return NotFound();

        // Departman adı ve lokasyonu herkes güncelleyebilir
        department.DepartmentName = model.DepartmentName;
        department.LocationId = model.LocationId;

        // Yönetici değişikliği sadece HR/Admin yapabilir
        if (isHRorAdmin && model.ManagerId != department.ManagerId)
        {
            var oldManagerId = department.ManagerId;
            var newManagerId = model.ManagerId;

            // Yeni manager'a rol ata
            var managerEmployee = _context.Employees
                .FirstOrDefault(e => e.EmployeeId == newManagerId);

            if (managerEmployee != null && managerEmployee.UserId != null)
            {
                int depManagerRoleId = _context.Roles
                    .Where(r => r.RoleName == "DepartmentManager" || r.RoleName == "Department Manager")
                    .Select(r => r.RoleId)
                    .FirstOrDefault();

                if (depManagerRoleId != 0)
                {
                    int userId = managerEmployee.UserId.Value;

                    bool exists = _context.UserRoles
                        .Any(x => x.UserId == userId && x.RoleId == depManagerRoleId);

                    if (!exists)
                    {
                        _context.UserRoles.Add(new UserRole
                        {
                            UserId = userId,
                            RoleId = depManagerRoleId,
                            AssignedDate = DateTime.Now
                        });
                    }
                }
            }

            // Yöneticiyi güncelle
            department.ManagerId = model.ManagerId;
        }
        // DepartmentManager ise yönetici değişikliğini yok say (model'den gelen değeri kullanma)

        _context.SaveChanges();

        TempData["SuccessMessage"] = "Departman başarıyla güncellendi!";
        return RedirectToAction("Index");
    }



    // SADECE HR/Admin silebilir
    public IActionResult Delete(int id)
    {
        var userRole = HttpContext.Session.GetString("UserRole");

        if (userRole != "HR" && userRole != "Admin")
        {
            TempData["ErrorMessage"] = "Departman silme yetkiniz yok!";
            return RedirectToAction("Index");
        }

        var dep = _context.Departments.Find(id);
        if (dep == null) return NotFound();

        _context.Departments.Remove(dep);
        _context.SaveChanges();
        
        TempData["SuccessMessage"] = "Departman başarıyla silindi!";
        return RedirectToAction("Index");
    }

    // SADECE HR/Admin departman ekleyebilir
    [HttpGet]
    public IActionResult Create()
    {
        var userRole = HttpContext.Session.GetString("UserRole");

        if (userRole != "HR" && userRole != "Admin")
        {
            TempData["ErrorMessage"] = "Departman ekleme yetkiniz yok!";
            return RedirectToAction("Index");
        }

        ViewBag.Locations = new SelectList(_context.Locations, "LocationId", "LocationName");
        ViewBag.Managers = new SelectList(_context.Employees, "EmployeeId", "FirstName");
        return View();
    }

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

        // ================================
        // 1) Manager seçildiyse rol ekle
        // ================================
        if (model.ManagerId.HasValue)
        {
            var managerEmployee = _context.Employees
                .FirstOrDefault(e => e.EmployeeId == model.ManagerId.Value);

            if (managerEmployee != null && managerEmployee.UserId.HasValue)
            {
                // Department Manager rol ID'sini bul
                int depManagerRoleId = _context.Roles
                    .Where(r => r.RoleName == "Department Manager")
                    .Select(r => r.RoleId)
                    .FirstOrDefault();

                if (depManagerRoleId != 0) // rol bulunduysa
                {
                    int userId = managerEmployee.UserId.Value;

                    // Bu kullanıcı zaten bu role sahip mi?
                    bool exists = _context.UserRoles
                        .Any(x => x.UserId == userId && x.RoleId == depManagerRoleId);

                    if (!exists)
                    {
                        _context.UserRoles.Add(new UserRole
                        {
                            UserId = userId,
                            RoleId = depManagerRoleId,
                            AssignedDate = DateTime.Now
                        });
                        // SaveChanges gerekmez, aşağıdaki SaveChanges ile kaydolur
                    }
                }
            }
        }

        // ================================
        // 2) Departmanı veritabanına ekle
        // ================================
        _context.Departments.Add(model);
        _context.SaveChanges();

        TempData["SuccessMessage"] = "Departman başarıyla eklendi!";
        return RedirectToAction("Index");
    }

    // Departman çalışanlarını görüntüleme
    public IActionResult Employees(int id)
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        var employeeId = HttpContext.Session.GetInt32("EmployeeId");

        var department = _context.Departments
            .Include(d => d.Location)
            .Include(d => d.Manager)
            .Include(d => d.Employees)
                .ThenInclude(e => e.Job)
            .Include(d => d.Employees)
                .ThenInclude(e => e.EmploymentContracts.Where(c => c.IsActive))
            .FirstOrDefault(d => d.DepartmentId == id);

        if (department == null)
            return NotFound();

        // DepartmentManager ise sadece kendi departmanının çalışanlarını görebilir
        var isDeptManager = userRole == "DepartmentManager" || userRole == "DepManager";
        if (isDeptManager && department.ManagerId != employeeId)
        {
            TempData["ErrorMessage"] = "Sadece yöneticisi olduğunuz departmanın çalışanlarını görüntüleyebilirsiniz!";
            return RedirectToAction("Index");
        }

        // Departman ID'sini ViewBag'e ekle
        ViewBag.DepartmentId = id;

        return View(department);
    }

}
