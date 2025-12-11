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
            const string sql = @"
                SELECT * FROM Departments
                WHERE ManagerID = {0}";

            departments = _context.Departments
                .FromSqlRaw(sql, employeeId)
                .Include(d => d.Location)
                .Include(d => d.Manager)
                .Include(d => d.Employees)
                .ToList();
        }
        // HR/Admin ise tüm departmanları göster
        else if (userRole == "HR" || userRole == "Admin")
        {
            const string sql = @"SELECT * FROM Departments";

            departments = _context.Departments
                .FromSqlRaw(sql)
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

        const string sqlDep = @"SELECT * FROM Departments WHERE DepartmentID = {0}";
        var dep = _context.Departments
            .FromSqlRaw(sqlDep, id)
            .Include(d => d.Employees) // departman çalışanlarına erişmek için
            .AsEnumerable()
            .FirstOrDefault();

        if (dep == null)
            return NotFound();

        // DepartmentManager ise sadece kendi departmanını düzenleyebilir
        if ((userRole == "Department Manager" || userRole == "DepManager") && dep.ManagerId != employeeId)
        {
            TempData["ErrorMessage"] = "Sadece yöneticisi olduğunuz departmanı düzenleyebilirsiniz!";
            return RedirectToAction("Index");
        }

        // Locations dropdown'u (SQL)
        const string sqlLoc = @"SELECT * FROM Locations";
        var locations = _context.Locations.FromSqlRaw(sqlLoc).ToList();
        ViewBag.Locations = new SelectList(locations, "LocationId", "LocationName", dep.LocationId);

        var isHRorAdmin = userRole == "HR" || userRole == "Admin";
        ViewBag.CanChangeManager = isHRorAdmin;

        if (isHRorAdmin)
        {
            // SADECE BU DEPARTMANA AİT ÇALIŞANLAR (SQL)
            const string sqlEmps = @"
                SELECT * FROM Employees
                WHERE DepartmentID = {0} AND IsActive = 1";

            var managers = _context.Employees
                .FromSqlRaw(sqlEmps, dep.DepartmentId)
                .AsEnumerable()
                .Select(e => new
                {
                    e.EmployeeId,
                    FullName = e.FirstName + " " + e.LastName
                })
                .ToList();

            ViewBag.Managers = new SelectList(
                managers,
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

        // Navigation property'leri ModelState'den temizle
        ModelState.Remove("Location");
        ModelState.Remove("Manager");
        ModelState.Remove("Employees");
        ModelState.Remove("DocumentPermissions");

        if (!ModelState.IsValid)
        {
            // Locations dropdown'u (SQL)
            const string sqlLoc = @"SELECT * FROM Locations";
            var locations = _context.Locations.FromSqlRaw(sqlLoc).ToList();
            ViewBag.Locations = new SelectList(locations, "LocationId", "LocationName", model.LocationId);

            ViewBag.CanChangeManager = isHRorAdmin;
            if (isHRorAdmin)
            {
                const string sqlEmps = @"
                    SELECT * FROM Employees
                    WHERE DepartmentID = {0} AND IsActive = 1";

                var managers = _context.Employees
                    .FromSqlRaw(sqlEmps, model.DepartmentId)
                    .AsEnumerable()
                    .Select(e => new { e.EmployeeId, FullName = e.FirstName + " " + e.LastName })
                    .ToList();

                ViewBag.Managers = new SelectList(
                    managers,
                    "EmployeeId",
                    "FullName",
                    model.ManagerId);
            }

            return View(model);
        }

        const string sqlDep = @"SELECT * FROM Departments WHERE DepartmentID = {0}";
        var department = _context.Departments
            .FromSqlRaw(sqlDep, model.DepartmentId)
            .AsEnumerable()
            .FirstOrDefault();

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
                const string sqlNewMgr = @"SELECT * FROM Employees WHERE EmployeeID = {0}";
                var newManagerEmployee = _context.Employees
                    .FromSqlRaw(sqlNewMgr, newManagerId.Value)
                    .AsEnumerable()
                    .FirstOrDefault();

                if (newManagerEmployee != null && newManagerEmployee.UserId.HasValue)
                {
                    const string sqlDepRole = @"
                        SELECT RoleID FROM Roles 
                        WHERE RoleName = 'Department Manager'";

                    int depManagerRoleId = _context.Database
                        .SqlQueryRaw<int>(sqlDepRole)
                        .AsEnumerable()
                        .FirstOrDefault();

                    if (depManagerRoleId != 0)
                    {
                        int newUserId = newManagerEmployee.UserId.Value;

                        const string sqlExists = @"
                            SELECT COUNT(*) AS Value
                            FROM UserRoles
                            WHERE UserID = {0} AND RoleID = {1}";

                        bool exists = _context.Database
                            .SqlQueryRaw<int>(sqlExists, newUserId, depManagerRoleId)
                            .AsEnumerable()
                            .FirstOrDefault() > 0;

                        if (!exists)
                        {
                            const string sqlInsertRole = @"
                                INSERT INTO UserRoles (UserID, RoleID, AssignedDate)
                                VALUES ({0}, {1}, GETDATE())";

                            _context.Database.ExecuteSqlRaw(sqlInsertRole, newUserId, depManagerRoleId);
                        }
                    }
                }
            }

            // 2) Eski manager'dan rolü gerekirse kaldır
            if (oldManagerId.HasValue)
            {
                const string sqlOldMgr = @"SELECT * FROM Employees WHERE EmployeeID = {0}";
                var oldManagerEmployee = _context.Employees
                    .FromSqlRaw(sqlOldMgr, oldManagerId.Value)
                    .AsEnumerable()
                    .FirstOrDefault();

                if (oldManagerEmployee != null && oldManagerEmployee.UserId.HasValue)
                {
                    const string sqlDepRole = @"
                        SELECT RoleID FROM Roles 
                        WHERE RoleName = 'Department Manager'";

                    int depManagerRoleId = _context.Database
                        .SqlQueryRaw<int>(sqlDepRole)
                        .AsEnumerable()
                        .FirstOrDefault();

                    if (depManagerRoleId != 0)
                    {
                        int oldUserId = oldManagerEmployee.UserId.Value;

                        const string sqlStillMgr = @"
                            SELECT COUNT(*) AS Value
                            FROM Departments
                            WHERE ManagerID = {0} AND DepartmentID <> {1}";

                        bool stillManagerSomewhere = _context.Database
                            .SqlQueryRaw<int>(sqlStillMgr, oldManagerEmployee.EmployeeId, department.DepartmentId)
                            .AsEnumerable()
                            .FirstOrDefault() > 0;

                        if (!stillManagerSomewhere)
                        {
                            const string sqlDeleteUserRole = @"
                                DELETE FROM UserRoles
                                WHERE UserID = {0} AND RoleID = {1}";

                            _context.Database.ExecuteSqlRaw(sqlDeleteUserRole, oldUserId, depManagerRoleId);
                        }
                    }
                }
            }

            // 3) Departman üzerindeki ManagerId'yi güncelle
            department.ManagerId = model.ManagerId;
        }

        // Department güncelle – SQL
        const string sqlUpdateDep = @"
            UPDATE Departments
            SET DepartmentName = {0}, LocationID = {1}, ManagerID = {2}
            WHERE DepartmentID = {3}";

        _context.Database.ExecuteSqlRaw(
            sqlUpdateDep,
            department.DepartmentName,
            department.LocationId,
            department.ManagerId,
            department.DepartmentId);

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

        const string sqlDep = @"SELECT * FROM Departments WHERE DepartmentID = {0}";
        var dep = _context.Departments
            .FromSqlRaw(sqlDep, id)
            .AsEnumerable()
            .FirstOrDefault();

        if (dep == null) return NotFound();

        const string sqlHasEmp = @"
            SELECT COUNT(*) AS Value
            FROM Employees
            WHERE DepartmentID = {0}";

        bool hasEmployee = _context.Database
            .SqlQueryRaw<int>(sqlHasEmp, id)
            .AsEnumerable()
            .FirstOrDefault() > 0;

        if (hasEmployee)
        {
            TempData["ErrorMessage"] = "Bu departmana bağlı çalışanlar var! Önce çalışanların departmanını değiştirin!!!";
            return RedirectToAction("Index", "HR");
        }

        const string sqlDeleteDep = @"DELETE FROM Departments WHERE DepartmentID = {0}";
        _context.Database.ExecuteSqlRaw(sqlDeleteDep, id);

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

        const string sqlLoc = @"SELECT * FROM Locations";
        var locations = _context.Locations.FromSqlRaw(sqlLoc).ToList();
        ViewBag.Locations = new SelectList(locations, "LocationId", "LocationName");

        const string sqlEmps = @"SELECT * FROM Employees";
        var emps = _context.Employees.FromSqlRaw(sqlEmps).ToList();
        ViewBag.Managers = new SelectList(emps, "EmployeeId", "FirstName");

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
            const string sqlLoc = @"SELECT * FROM Locations";
            var locations = _context.Locations.FromSqlRaw(sqlLoc).ToList();
            ViewBag.Locations = new SelectList(locations, "LocationId", "LocationName", model.LocationId);

            return View(model);
        }

        // Departman oluştururken yöneticiyi null yap
        model.ManagerId = null;

        const string sqlInsertDep = @"
            INSERT INTO Departments (DepartmentName, LocationID, ManagerID)
            VALUES ({0}, {1}, NULL)";

        _context.Database.ExecuteSqlRaw(
            sqlInsertDep,
            model.DepartmentName,
            model.LocationId);

        TempData["SuccessMessage"] = "Departman başarıyla eklendi!";
        return RedirectToAction("Index");
    }

    // Departman çalışanlarını görüntüleme
    public IActionResult Employees(int id)
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        var employeeId = HttpContext.Session.GetInt32("EmployeeId");

        const string sqlDep = @"SELECT * FROM Departments WHERE DepartmentID = {0}";
        var department = _context.Departments
            .FromSqlRaw(sqlDep, id)
            .Include(d => d.Location)
            .Include(d => d.Manager)
            .Include(d => d.Employees)
                .ThenInclude(e => e.Job)
            .Include(d => d.Employees)
                .ThenInclude(e => e.EmploymentContracts.Where(c => c.IsActive))
            .AsEnumerable()
            .FirstOrDefault();

        if (department == null)
            return NotFound();

        // DepartmentManager ise sadece kendi departmanının çalışanlarını görebilir
        var isDeptManager = userRole == "Department Manager" || userRole == "DepManager";
        if (isDeptManager && department.ManagerId != employeeId)
        {
            TempData["ErrorMessage"] = "Sadece yöneticisi olduğunuz departmanın çalışanlarını görüntüleyebilirsiniz!";
            return RedirectToAction("Index");
        }

        ViewBag.DepartmentId = id;

        return View(department);
    }
}