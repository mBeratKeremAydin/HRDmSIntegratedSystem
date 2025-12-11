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

        if (userRole == null)
        {
            return RedirectToAction("Index", "Login");
        }

        List<Department> departments;

        // DepartmentManager ise sadece yöneticisi olduğu departmanları göster
        if (userRole == "Department Manager" || userRole == "DepManager")
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

        var dep = _context.Departments
            .Include(d => d.Employees) // departman çalışanlarına erişmek için
            .FirstOrDefault(d => d.DepartmentId == id);

        if (dep == null)
            return NotFound();

        // DepartmentManager ise sadece kendi departmanını düzenleyebilir
        if ((userRole == "Department Manager" || userRole == "DepManager") && dep.ManagerId != employeeId)
        {
            TempData["ErrorMessage"] = "Sadece yöneticisi olduğunuz departmanı düzenleyebilirsiniz!";
            return RedirectToAction("Index");
        }

        ViewBag.Locations = new SelectList(_context.Locations, "LocationId", "LocationName", dep.LocationId);

        var isHRorAdmin = userRole == "HR" || userRole == "Admin";
        ViewBag.CanChangeManager = isHRorAdmin;

        if (isHRorAdmin)
        {
            // SADECE BU DEPARTMANA AİT ÇALIŞANLAR
            ViewBag.Managers = new SelectList(
                _context.Employees
                    .Where(e => e.DepartmentId == dep.DepartmentId && e.IsActive)
                    .Select(e => new
                    {
                        e.EmployeeId,
                        FullName = e.FirstName + " " + e.LastName
                    }),
                "EmployeeId",
                "FullName",
                dep.ManagerId);
        }

        return View(dep);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(Department model)
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        var isHRorAdmin = userRole == "HR" || userRole == "Admin";

        // ✅ DEPARTMENT İÇİN: Navigation property'leri ModelState'den temizle
        ModelState.Remove("Location");
        ModelState.Remove("Manager");
        ModelState.Remove("Employees");
        ModelState.Remove("DocumentPermissions");

        if (!ModelState.IsValid)
        {
            ViewBag.Locations = new SelectList(_context.Locations, "LocationId", "LocationName", model.LocationId);
            ViewBag.CanChangeManager = isHRorAdmin;
            if (isHRorAdmin)
            {
                ViewBag.Managers = new SelectList(
                    _context.Employees
                        .Where(e => e.DepartmentId == model.DepartmentId && e.IsActive)
                        .Select(e => new { e.EmployeeId, FullName = e.FirstName + " " + e.LastName }),
                    "EmployeeId", "FullName", model.ManagerId);
            }
            return View(model);
        }

        var department = _context.Departments
            .FirstOrDefault(d => d.DepartmentId == model.DepartmentId);

        if (department == null)
            return NotFound();

        department.DepartmentName = model.DepartmentName;
        department.LocationId = model.LocationId;

        if (isHRorAdmin && model.ManagerId != department.ManagerId)
        {
            var oldManagerId = department.ManagerId;
            var newManagerId = model.ManagerId;

            // 1) Yeni manager'a DepartmentManager rolü ver
            if (newManagerId.HasValue)
            {
                var newManagerEmployee = _context.Employees
                    .FirstOrDefault(e => e.EmployeeId == newManagerId.Value);

                if (newManagerEmployee != null && newManagerEmployee.UserId.HasValue)
                {
                    int depManagerRoleId = _context.Roles
                        .Where(r => r.RoleName == "Department Manager" || r.RoleName == "Department Manager")
                        .Select(r => r.RoleId)
                        .FirstOrDefault();

                    if (depManagerRoleId != 0)
                    {
                        int newUserId = newManagerEmployee.UserId.Value;

                        bool exists = _context.UserRoles
                            .Any(x => x.UserId == newUserId && x.RoleId == depManagerRoleId);

                        if (!exists)
                        {
                            _context.UserRoles.Add(new UserRole
                            {
                                UserId = newUserId,
                                RoleId = depManagerRoleId,
                                AssignedDate = DateTime.Now
                            });
                        }
                    }
                }
            }

            // 2) Eski manager'dan rolü gerekirse kaldır
            if (oldManagerId.HasValue)
            {
                var oldManagerEmployee = _context.Employees
                    .FirstOrDefault(e => e.EmployeeId == oldManagerId.Value);

                if (oldManagerEmployee != null && oldManagerEmployee.UserId.HasValue)
                {
                    int depManagerRoleId = _context.Roles
                        .Where(r => r.RoleName == "Department Manager" || r.RoleName == "Department Manager")
                        .Select(r => r.RoleId)
                        .FirstOrDefault();

                    if (depManagerRoleId != 0)
                    {
                        int oldUserId = oldManagerEmployee.UserId.Value;

                        // Bu kullanıcı halen başka bir departmanın yöneticisi mi?
                        bool stillManagerSomewhere = _context.Departments
                            .Any(d => d.ManagerId == oldManagerEmployee.EmployeeId && d.DepartmentId != department.DepartmentId);

                        if (!stillManagerSomewhere)
                        {
                            var userRoleToRemove = _context.UserRoles
                                .FirstOrDefault(ur => ur.UserId == oldUserId && ur.RoleId == depManagerRoleId);

                            if (userRoleToRemove != null)
                            {
                                _context.UserRoles.Remove(userRoleToRemove);
                            }
                        }
                    }
                }
            }

            // 3) Departman üzerindeki ManagerId'yi güncelle
            department.ManagerId = model.ManagerId;
        }

        _context.SaveChanges();

        TempData["SuccessMessage"] = "Departman başarıyla güncellendi!";
        return RedirectToAction("Index");
    }



    // SADECE HR silebilir
    public IActionResult Delete(int id)
    {
        var userRole = HttpContext.Session.GetString("UserRole");

        if (userRole != "HR")
        {
            TempData["ErrorMessage"] = "Departman silme yetkiniz yok!";
            return RedirectToAction("Index");
        }

        var dep = _context.Departments.Find(id);
        if (dep == null) return NotFound();

        var hasEmployee = _context.Employees.Any(e => e.DepartmentId == id);

        if (hasEmployee)
        {
            TempData["ErrorMessage"] = "Bu departmana bağlı çalışanlar var! Önce çalışanların departmanını değiştirin!!!";
            return RedirectToAction("Index","HR");
        }

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

        ModelState.Remove("Location");
        ModelState.Remove("Manager");
        ModelState.Remove("Employees");
        ModelState.Remove("DocumentPermissions");

        if (!ModelState.IsValid)
        {
            ViewBag.Locations = new SelectList(_context.Locations, "LocationId", "LocationName", model.LocationId);
            
            return View(model);
        }

        // YENİ: Departman oluştururken ZORUNLU OLARAK yöneticiyi null yap
        model.ManagerId = null;

        // ManagerId null olduğu için rol atama kısmını tamamen kaldırıyoruz / atlamış oluyoruz

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
        var isDeptManager = userRole == "Department Manager" || userRole == "Department Manager";
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
