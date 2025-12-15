using HRDms.Data.Context;
using HR.Mvc.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HR.Mvc.Controllers
{
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

            string sql = @"
                SELECT 
                    d.DepartmentID, 
                    d.DepartmentName, 
                    COALESCE(l.LocationName, '') AS LocationName,
                    COALESCE(m.FirstName + ' ' + m.LastName, '') AS ManagerName,
                    (SELECT COUNT(*) FROM Employees e WHERE e.DepartmentID = d.DepartmentID) AS EmployeeCount
                FROM Departments d
                LEFT JOIN Locations l ON d.LocationID = l.LocationID
                LEFT JOIN Employees m ON d.ManagerID = m.EmployeeID";

            // DepartmentManager ise sadece yöneticisi olduğu departmanları göster
            if (userRole == "Department Manager" || userRole == "DepManager")
            {
                sql += $" WHERE d.ManagerID = {employeeId}";
            }
            // HR/Admin ise tüm departmanları göster
            else if (userRole != "HR" && userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Bu sayfayı görüntüleme yetkiniz yok!";
                return RedirectToAction("Index", "Employee");
            }

            var departments = _context.Database.SqlQueryRaw<DepartmentViewModel>(sql).ToList();

            return View(departments);
        }

        // HR + DepartmentManager düzenleyebilir
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var employeeId = HttpContext.Session.GetInt32("EmployeeId");

            const string sqlDep = @"
                SELECT 
                    d.DepartmentID, 
                    d.DepartmentName, 
                    d.LocationID, 
                    d.ManagerID,
                    COALESCE(m.FirstName + ' ' + m.LastName, '') AS ManagerName
                FROM Departments d
                LEFT JOIN Employees m ON d.ManagerID = m.EmployeeID
                WHERE d.DepartmentID = {0}";
            
            var dep = _context.Database.SqlQueryRaw<DepartmentCreateViewModel>(sqlDep, id)
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
            const string sqlLoc = @"SELECT LocationID, LocationName FROM Locations";
            var locations = _context.Database.SqlQueryRaw<LocationDTO>(sqlLoc).ToList();
            ViewBag.Locations = new SelectList(locations, "LocationId", "LocationName", dep.LocationId);

            var isHRorAdmin = userRole == "HR" || userRole == "Admin";
            ViewBag.CanChangeManager = isHRorAdmin;

            if (isHRorAdmin) // sadece hr ve admin editleyebilir
            {
                
                const string sqlEmps = @"
                    SELECT EmployeeID, FirstName + ' ' + LastName AS FullName 
                    FROM Employees 
                    WHERE IsActive = 1 AND DepartmentID = {0}";

                var managers = _context.Database.SqlQueryRaw<EmployeeSelectDTO>(sqlEmps, id).ToList();

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
        public IActionResult Edit(DepartmentCreateViewModel model)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var isHRorAdmin = userRole == "HR" || userRole == "Admin";

            if (!ModelState.IsValid)
            {
                const string sqlLoc = @"SELECT LocationID, LocationName FROM Locations";
                var locations = _context.Database.SqlQueryRaw<LocationDTO>(sqlLoc).ToList();
                ViewBag.Locations = new SelectList(locations, "LocationId", "LocationName", model.LocationId);

                ViewBag.CanChangeManager = isHRorAdmin;
                if (isHRorAdmin)
                {
                    const string sqlEmps = @"
                        SELECT EmployeeID, FirstName + ' ' + LastName AS FullName 
                        FROM Employees 
                        WHERE IsActive = 1 AND DepartmentID = {0}";

                    var managers = _context.Database.SqlQueryRaw<EmployeeSelectDTO>(sqlEmps, model.DepartmentId).ToList();

                    ViewBag.Managers = new SelectList(
                        managers,
                        "EmployeeId",
                        "FullName",
                        model.ManagerId);
                }

                return View(model);
            }

            // Mevcut departman bilgisini al (Eski yöneticiyi bulmak için)
            const string sqlCurrentDep = @"SELECT ManagerID FROM Departments WHERE DepartmentID = {0}";
            var currentManagerId = _context.Database.SqlQueryRaw<int?>(sqlCurrentDep, model.DepartmentId).AsEnumerable().FirstOrDefault();

            if (isHRorAdmin && model.ManagerId != currentManagerId)
            {
                var oldManagerId = currentManagerId;
                var newManagerId = model.ManagerId;

                // 1) Yeni manager'a DepartmentManager rolü ver
                if (newManagerId.HasValue)
                {
                    const string sqlNewMgrUserId = @"SELECT UserId FROM Employees WHERE EmployeeID = {0}";
                    var newUserId = _context.Database.SqlQueryRaw<int?>(sqlNewMgrUserId, newManagerId.Value).AsEnumerable().FirstOrDefault();

                    if (newUserId.HasValue)
                    {
                        const string sqlDepRole = @"SELECT RoleID FROM Roles WHERE RoleName = 'Department Manager'";
                        int depManagerRoleId = _context.Database.SqlQueryRaw<int>(sqlDepRole).AsEnumerable().FirstOrDefault();

                        if (depManagerRoleId != 0)
                        {
                            const string sqlExists = @"SELECT COUNT(*) FROM UserRoles WHERE UserID = {0} AND RoleID = {1}";
                            bool exists = _context.Database.SqlQueryRaw<int>(sqlExists, newUserId.Value, depManagerRoleId).AsEnumerable().FirstOrDefault() > 0;

                            if (!exists)
                            {
                                const string sqlInsertRole = @"INSERT INTO UserRoles (UserID, RoleID, AssignedDate) VALUES ({0}, {1}, GETDATE())";
                                _context.Database.ExecuteSqlRaw(sqlInsertRole, newUserId.Value, depManagerRoleId);
                            }
                        }
                    }
                }

                // 2) Eski manager'dan rolü gerekirse kaldır
                if (oldManagerId.HasValue)
                {
                    const string sqlOldMgrUserId = @"SELECT UserId FROM Employees WHERE EmployeeID = {0}";
                    var oldUserId = _context.Database.SqlQueryRaw<int?>(sqlOldMgrUserId, oldManagerId.Value).AsEnumerable().FirstOrDefault();

                    if (oldUserId.HasValue)
                    {
                        const string sqlDepRole = @"SELECT RoleID FROM Roles WHERE RoleName = 'Department Manager'";
                        int depManagerRoleId = _context.Database.SqlQueryRaw<int>(sqlDepRole).AsEnumerable().FirstOrDefault();

                        if (depManagerRoleId != 0)
                        {
                            // Başka bir departmanın yöneticisi mi?
                            const string sqlStillMgr = @"
                                SELECT COUNT(*) 
                                FROM Departments 
                                WHERE ManagerID = {0} AND DepartmentID <> {1}";

                            bool stillManagerSomewhere = _context.Database.SqlQueryRaw<int>(sqlStillMgr, oldManagerId.Value, model.DepartmentId).AsEnumerable().FirstOrDefault() > 0;

                            if (!stillManagerSomewhere)
                            {
                                const string sqlDeleteUserRole = @"DELETE FROM UserRoles WHERE UserID = {0} AND RoleID = {1}";
                                _context.Database.ExecuteSqlRaw(sqlDeleteUserRole, oldUserId.Value, depManagerRoleId);
                            }
                        }
                    }
                }
            }

            // Department güncelle – SQL
            const string sqlUpdateDep = @"
                UPDATE Departments
                SET DepartmentName = {0}, LocationID = {1}, ManagerID = {2}
                WHERE DepartmentID = {3}";

            _context.Database.ExecuteSqlRaw(
                sqlUpdateDep,
                model.DepartmentName,
                model.LocationId,
                model.ManagerId,
                model.DepartmentId);

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

            const string sqlHasEmp = @"SELECT COUNT(*) FROM Employees WHERE DepartmentID = {0}";
            bool hasEmployee = _context.Database.SqlQueryRaw<int>(sqlHasEmp, id).AsEnumerable().FirstOrDefault() > 0;

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

            const string sqlLoc = @"SELECT LocationID, LocationName FROM Locations";
            var locations = _context.Database.SqlQueryRaw<LocationDTO>(sqlLoc).ToList();
            ViewBag.Locations = new SelectList(locations, "LocationId", "LocationName");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(DepartmentCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                const string sqlLoc = @"SELECT LocationID, LocationName FROM Locations";
                var locations = _context.Database.SqlQueryRaw<LocationDTO>(sqlLoc).ToList();
                ViewBag.Locations = new SelectList(locations, "LocationId", "LocationName", model.LocationId);

                return View(model);
            }

            const string sqlInsertDep = @"
                INSERT INTO Departments (DepartmentName, LocationID, ManagerID)
                VALUES ({0}, {1}, {2})";

            _context.Database.ExecuteSqlRaw(
                sqlInsertDep,
                model.DepartmentName,
                model.LocationId,
                null); // ManagerID otomatik NULL 

            TempData["SuccessMessage"] = "Departman başarıyla eklendi!";
            return RedirectToAction("Index");
        }

        // Departman çalışanlarını görüntüleme
        public IActionResult Employees(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var employeeId = HttpContext.Session.GetInt32("EmployeeId");

            const string sqlDep = @"
                SELECT 
                    d.DepartmentID, 
                    d.DepartmentName, 
                    COALESCE(l.LocationName, '') AS LocationName,
                    COALESCE(m.FirstName + ' ' + m.LastName, '') AS ManagerName
                FROM Departments d
                LEFT JOIN Locations l ON d.LocationID = l.LocationID
                LEFT JOIN Employees m ON d.ManagerID = m.EmployeeID
                WHERE d.DepartmentID = {0}";

            var department = _context.Database.SqlQueryRaw<DepartmentDetailViewModel>(sqlDep, id)
                .AsEnumerable()
                .FirstOrDefault();

            if (department == null)
                return NotFound();

            
            if (userRole == "Department Manager" || userRole == "DepManager")
            {
                 const string sqlCheckMgr = "SELECT ManagerID FROM Departments WHERE DepartmentID = {0}";
                 var mgrId = _context.Database.SqlQueryRaw<int?>(sqlCheckMgr, id).AsEnumerable().FirstOrDefault();
                 
                 if (mgrId != employeeId)
                 {
                    TempData["ErrorMessage"] = "Sadece yöneticisi olduğunuz departmanın çalışanlarını görüntüleyebilirsiniz!";
                    return RedirectToAction("Index");
                 }
            }

            const string sqlEmps = @"
                SELECT 
                    e.EmployeeID, 
                    e.FirstName + ' ' + e.LastName AS FullName, 
                    COALESCE(j.JobTitle, '') AS JobTitle, 
                    e.Email,
                    COALESCE(e.PhoneNumber, '') AS PhoneNumber,
                    e.HireDate,
                    e.IsActive
                FROM Employees e
                LEFT JOIN Jobs j ON e.JobID = j.JobID
                WHERE e.DepartmentID = {0}";

            department.Employees = _context.Database.SqlQueryRaw<DepartmentEmployeeDTO>(sqlEmps, id).ToList();

            ViewBag.DepartmentId = id;

            return View(department);
        }
    }
}