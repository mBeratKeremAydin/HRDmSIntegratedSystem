using HRDms.Data.Context;
using HRDms.Data.Models;
using HR.Mvc.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HR.Mvc.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // Ana Dashboard – TAMAMEN SQL
        public IActionResult Index()
        {
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Bu sayfaya erişim yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            string sqlTotalEmployees = "SELECT COUNT(*) AS Value FROM Employees";
            string sqlHREmployees = @"
                SELECT COUNT(*) AS Value
                FROM Employees e
                JOIN Departments d ON e.DepartmentID = d.DepartmentID
                WHERE d.DepartmentName = 'HR'";
            string sqlTotalJobs = "SELECT COUNT(*) AS Value FROM Jobs";
            string sqlTotalLeaveTypes = "SELECT COUNT(*) AS Value FROM LeaveTypes";
            string sqlTotalLocations = "SELECT COUNT(*) AS Value FROM Locations";
            string sqlTotalRoles = "SELECT COUNT(*) AS Value FROM Roles";
            string sqlTotalDepartments = "SELECT COUNT(*) AS Value FROM Departments";

            ViewBag.TotalEmployees = _context.Database.SqlQueryRaw<int>(sqlTotalEmployees).AsEnumerable().FirstOrDefault();
            ViewBag.TotalHREmployees = _context.Database.SqlQueryRaw<int>(sqlHREmployees).AsEnumerable().FirstOrDefault();
            ViewBag.TotalJobs = _context.Database.SqlQueryRaw<int>(sqlTotalJobs).AsEnumerable().FirstOrDefault();
            ViewBag.TotalLeaveTypes = _context.Database.SqlQueryRaw<int>(sqlTotalLeaveTypes).AsEnumerable().FirstOrDefault();
            ViewBag.TotalLocations = _context.Database.SqlQueryRaw<int>(sqlTotalLocations).AsEnumerable().FirstOrDefault();
            ViewBag.TotalRoles = _context.Database.SqlQueryRaw<int>(sqlTotalRoles).AsEnumerable().FirstOrDefault();
            ViewBag.TotalDepartments = _context.Database.SqlQueryRaw<int>(sqlTotalDepartments).AsEnumerable().FirstOrDefault();

            return View();
        }

        #region HR Employee Management

        // HR Çalışanları Listele – SQL
        public IActionResult HREmployees()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            // HR Departman ID'sini bul
            const string sqlFindHRId = "SELECT DepartmentID AS Value FROM Departments WHERE DepartmentName = 'HR'";
            var hrDepartmentId = _context.Database.SqlQueryRaw<int>(sqlFindHRId).AsEnumerable().FirstOrDefault();

            if (hrDepartmentId == 0)
            {
                TempData["ErrorMessage"] = "HR departmanı bulunamadı!";
                return RedirectToAction("Index");
            }

            // HR Çalışanlarını ve JobTitle'ı getir
            string sqlHREmployees = @"
                SELECT 
                    e.EmployeeID, 
                    e.FirstName, 
                    e.LastName, 
                    e.Email, 
                    e.PhoneNumber, 
                    j.JobTitle, 
                    e.IsActive 
                FROM Employees e
                LEFT JOIN Jobs j ON e.JobID = j.JobID
                WHERE e.DepartmentID = {0}";

            var hrEmployees = _context.Database
                .SqlQueryRaw<HREmployeeViewModel>(sqlHREmployees, hrDepartmentId)
                .ToList();

            return View(hrEmployees);
        }

        // HR Çalışan Ekleme - GET – SQL
        [HttpGet]
        public IActionResult CreateHREmployee()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            const string sqlFindHR = "SELECT * FROM Departments WHERE DepartmentName = 'HR'";
            var hrDepartment = _context.Departments
                .FromSqlRaw(sqlFindHR)
                .AsEnumerable()
                .FirstOrDefault();

            if (hrDepartment == null)
            {
                TempData["ErrorMessage"] = "HR departmanı bulunamadı! Önce HR departmanı oluşturun.";
                return RedirectToAction("Index");
            }

            ViewBag.HRDepartmentId = hrDepartment.DepartmentId;
            ViewBag.HRDepartmentName = hrDepartment.DepartmentName;

            const string sqlJobs = "SELECT * FROM Jobs";
            var jobs = _context.Jobs.FromSqlRaw(sqlJobs).ToList();
            ViewBag.Jobs = new SelectList(jobs, "JobId", "JobTitle");

            return View();
        }

        // HR Çalışan Ekleme - POST – SQL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateHREmployee(Employee employee, string username, string userPassword)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            const string sqlFindHR = "SELECT * FROM Departments WHERE DepartmentName = 'HR'";
            var hrDepartment = _context.Departments
                .FromSqlRaw(sqlFindHR)
                .AsEnumerable()
                .FirstOrDefault();

            if (hrDepartment == null)
            {
                TempData["ErrorMessage"] = "HR departmanı bulunamadı!";
                return RedirectToAction("Index");
            }

            employee.DepartmentId = hrDepartment.DepartmentId;

            // Navigation temizle
            ModelState.Remove("Department");
            ModelState.Remove("Job");
            ModelState.Remove("Manager");
            ModelState.Remove("User");
            ModelState.Remove("Documents");
            ModelState.Remove("EmploymentContracts");
            ModelState.Remove("Attendances");
            ModelState.Remove("Departments");
            ModelState.Remove("InverseManager");
            ModelState.Remove("LeaveRequests");
            ModelState.Remove("PerformanceReviewEmployees");
            ModelState.Remove("PerformanceReviewReviewers");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(userPassword))
            {
                ModelState.AddModelError("", "Kullanıcı adı ve şifre zorunludur!");
                LoadJobsForHrCreate(employee.JobId, hrDepartment);
                return View(employee);
            }

            // Username kontrolü – SQL
            const string sqlCheckUsername = "SELECT COUNT(*) AS Value FROM Users WHERE Username = {0}";
            var usernameExists = _context.Database
                .SqlQueryRaw<int>(sqlCheckUsername, username)
                .AsEnumerable()
                .FirstOrDefault();

            if (usernameExists > 0)
            {
                ModelState.AddModelError("", "Bu kullanıcı adı zaten kullanılıyor!");
                LoadJobsForHrCreate(employee.JobId, hrDepartment);
                return View(employee);
            }

            // Email kontrolü – SQL
            if (!string.IsNullOrEmpty(employee.Email))
            {
                const string sqlCheckEmail = "SELECT COUNT(*) AS Value FROM Users WHERE Email = {0}";
                var emailExists = _context.Database
                    .SqlQueryRaw<int>(sqlCheckEmail, employee.Email)
                    .AsEnumerable()
                    .FirstOrDefault();

                if (emailExists > 0)
                {
                    ModelState.AddModelError("", "Bu email adresi zaten kullanılıyor!");
                    LoadJobsForHrCreate(employee.JobId, hrDepartment);
                    return View(employee);
                }
            }

            if (!ModelState.IsValid)
            {
                LoadJobsForHrCreate(employee.JobId, hrDepartment);
                return View(employee);
            }

            try
            {
                // User INSERT – SQL
                const string sqlInsertUser = @"
                    INSERT INTO Users (Username, UserPassword, Email, IsActive)
                    VALUES ({0}, {1}, {2}, {3});
                    SELECT CAST(SCOPE_IDENTITY() AS int);";

                var newUserId = _context.Database
                    .SqlQueryRaw<int>(sqlInsertUser, username, userPassword, employee.Email, employee.IsActive)
                    .AsEnumerable()
                    .First();

                employee.UserId = newUserId;

                // Employee INSERT – SQL
                const string sqlInsertEmployee = @"
                    INSERT INTO Employees 
                        (FirstName, LastName, Email, PhoneNumber, IdentityNumber, HireDate, 
                         DepartmentID, JobID, ManagerID, UserID, IsActive)
                    VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10});
                    SELECT CAST(SCOPE_IDENTITY() AS int);";

                _context.Database
                    .SqlQueryRaw<int>(
                        sqlInsertEmployee,
                        employee.FirstName,
                        employee.LastName,
                        employee.Email,
                        employee.PhoneNumber,
                        employee.IdentityNumber,
                        employee.HireDate,
                        employee.DepartmentId,
                        employee.JobId,
                        employee.ManagerId,
                        employee.UserId,
                        employee.IsActive)
                    .AsEnumerable()
                    .First();

                // Role ID’leri – SQL
                const string sqlFindHrRole = "SELECT RoleID FROM Roles WHERE RoleName = 'HR'";
                var hrRoleId = _context.Database.SqlQueryRaw<int>(sqlFindHrRole).AsEnumerable().FirstOrDefault();

                const string sqlFindEmployeeRole = "SELECT RoleID FROM Roles WHERE RoleName = 'Employee'";
                var employeeRoleId = _context.Database.SqlQueryRaw<int>(sqlFindEmployeeRole).AsEnumerable().FirstOrDefault();

                if (hrRoleId != 0)
                {
                    const string sqlInsertHrRole = @"
                        INSERT INTO UserRoles (UserID, RoleID, AssignedDate)
                        VALUES ({0}, {1}, GETDATE())";
                    _context.Database.ExecuteSqlRaw(sqlInsertHrRole, newUserId, hrRoleId);
                }

                if (employeeRoleId != 0)
                {
                    const string sqlInsertEmpRole = @"
                        INSERT INTO UserRoles (UserID, RoleID, AssignedDate)
                        VALUES ({0}, {1}, GETDATE())";
                    _context.Database.ExecuteSqlRaw(sqlInsertEmpRole, newUserId, employeeRoleId);
                }

                TempData["SuccessMessage"] = "HR çalışanı başarıyla eklendi!";
                return RedirectToAction("HREmployees");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                LoadJobsForHrCreate(employee.JobId, hrDepartment);
                return View(employee);
            }
        }

        private void LoadJobsForHrCreate(int? selectedJobId, Department hrDepartment)
        {
            ViewBag.HRDepartmentId = hrDepartment.DepartmentId;
            ViewBag.HRDepartmentName = hrDepartment.DepartmentName;

            const string sqlJobs = "SELECT * FROM Jobs";
            var jobs = _context.Jobs.FromSqlRaw(sqlJobs).ToList();
            ViewBag.Jobs = new SelectList(jobs, "JobId", "JobTitle", selectedJobId);
        }

        // HR Çalışan Silme – SQL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteHREmployee(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }
            
            const string sqlFindEmp = "SELECT * FROM Employees WHERE EmployeeID = {0}";
            var employee = _context.Employees
                .FromSqlRaw(sqlFindEmp, id)
                .AsEnumerable()
                .FirstOrDefault();

            if (employee == null)
            {
                TempData["ErrorMessage"] = "Çalışan bulunamadı!";
                return RedirectToAction("HREmployees");
            }

            // Kendi kendini silmeyi engelle (UserId üzerinden)
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId.HasValue && employee.UserId == currentUserId.Value)
            {
                TempData["ErrorMessage"] = "Güvenlik nedeniyle kendi hesabınızı silemezsiniz!";
                return RedirectToAction("HREmployees");
            }

            try
            {
                const string sqlNullSubordinates = "UPDATE Employees SET ManagerID = NULL WHERE ManagerID = {0}";
                _context.Database.ExecuteSqlRaw(sqlNullSubordinates, id);

                const string sqlNullReviews = "UPDATE PerformanceReviews SET ReviewerID = NULL WHERE ReviewerID = {0}";
                _context.Database.ExecuteSqlRaw(sqlNullReviews, id);

                // 1) Employee kaydını sil
                const string sqlDeleteEmp = "DELETE FROM Employees WHERE EmployeeID = {0}";
                _context.Database.ExecuteSqlRaw(sqlDeleteEmp, id);

                if (employee.UserId.HasValue)
                {
                    // 2) UserRoles kayıtlarını sil
                    const string sqlDeleteUserRoles = "DELETE FROM UserRoles WHERE UserID = {0}";
                    _context.Database.ExecuteSqlRaw(sqlDeleteUserRoles, employee.UserId);

                    // 3) Kullanıcıyı sil
                    const string sqlDeleteUser = "DELETE FROM Users WHERE UserID = {0}";
                    _context.Database.ExecuteSqlRaw(sqlDeleteUser, employee.UserId);
                }

                TempData["SuccessMessage"] = "HR çalışanı başarıyla silindi!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Silme işlemi başarısız: " + ex.Message;
            }

            return RedirectToAction("HREmployees");
        }

        #endregion

        #region Jobs Management

        // Jobs Listele – SQL
        public IActionResult Jobs()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            const string sql = "SELECT * FROM Jobs";
            var jobs = _context.Jobs.FromSqlRaw(sql).ToList();
            return View(jobs);
        }

        [HttpGet]
        public IActionResult CreateJob()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            return View();
        }

        // Job Ekle – SQL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateJob(Job job)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            ModelState.Remove("Employees");

            if (!ModelState.IsValid)
            {
                return View(job);
            }

            try
            {
                const string sql = @"
                    INSERT INTO Jobs (JobTitle, MinSalary, MaxSalary)
                    VALUES ({0}, {1}, {2})";

                _context.Database.ExecuteSqlRaw(sql, job.JobTitle, job.MinSalary, job.MaxSalary);

                TempData["SuccessMessage"] = "İş pozisyonu başarıyla eklendi!";
                return RedirectToAction("Jobs");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(job);
            }
        }

        // Job Düzenle – GET – SQL
        [HttpGet]
        public IActionResult EditJob(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            const string sql = "SELECT * FROM Jobs WHERE JobID = {0}";
            var job = _context.Jobs.FromSqlRaw(sql, id).AsEnumerable().FirstOrDefault();

            if (job == null)
            {
                TempData["ErrorMessage"] = "İş pozisyonu bulunamadı!";
                return RedirectToAction("Jobs");
            }

            return View(job);
        }

        // Job Düzenle – POST – SQL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditJob(Job job)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            ModelState.Remove("Employees");

            if (!ModelState.IsValid)
            {
                return View(job);
            }

            try
            {
                const string sql = @"
                    UPDATE Jobs
                    SET JobTitle = {0}, MinSalary = {1}, MaxSalary = {2}
                    WHERE JobID = {3}";

                _context.Database.ExecuteSqlRaw(sql, job.JobTitle, job.MinSalary, job.MaxSalary, job.JobId);

                TempData["SuccessMessage"] = "İş pozisyonu başarıyla güncellendi!";
                return RedirectToAction("Jobs");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(job);
            }
        }

        // Job Sil – SQL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteJob(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            const string sqlCheck = "SELECT COUNT(*) AS Value FROM Employees WHERE JobID = {0}";
            var hasEmployees = _context.Database.SqlQueryRaw<int>(sqlCheck, id).AsEnumerable().FirstOrDefault();

            if (hasEmployees > 0)
            {
                TempData["ErrorMessage"] = "Bu iş pozisyonuna bağlı çalışanlar var! Önce çalışanların pozisyonunu değiştirin.";
                return RedirectToAction("Jobs");
            }

            try
            {
                const string sql = "DELETE FROM Jobs WHERE JobID = {0}";
                _context.Database.ExecuteSqlRaw(sql, id);

                TempData["SuccessMessage"] = "İş pozisyonu başarıyla silindi!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Silme işlemi başarısız: " + ex.Message;
            }

            return RedirectToAction("Jobs");
        }

        #endregion

        #region Leave Types Management

        // LeaveTypes Listele – SQL
        public IActionResult LeaveTypes()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            const string sql = "SELECT * FROM LeaveTypes";
            var leaveTypes = _context.LeaveTypes.FromSqlRaw(sql).ToList();
            return View(leaveTypes);
        }

        [HttpGet]
        public IActionResult CreateLeaveType()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            return View();
        }

        // LeaveType Ekle – SQL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateLeaveType(LeaveType leaveType)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            ModelState.Remove("LeaveRequests");

            if (!ModelState.IsValid)
            {
                return View(leaveType);
            }

            try
            {
                const string sql = @"
                    INSERT INTO LeaveTypes (TypeName, DaysAllowed)
                    VALUES ({0}, {1})";

                _context.Database.ExecuteSqlRaw(sql, leaveType.TypeName, leaveType.DaysAllowed);

                TempData["SuccessMessage"] = "İzin türü başarıyla eklendi!";
                return RedirectToAction("LeaveTypes");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(leaveType);
            }
        }

        // EditLeaveType – GET – SQL
        [HttpGet]
        public IActionResult EditLeaveType(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            const string sql = "SELECT * FROM LeaveTypes WHERE LeaveTypeID = {0}";
            var leaveType = _context.LeaveTypes.FromSqlRaw(sql, id).AsEnumerable().FirstOrDefault();

            if (leaveType == null)
            {
                TempData["ErrorMessage"] = "İzin türü bulunamadı!";
                return RedirectToAction("LeaveTypes");
            }

            return View(leaveType);
        }

        // EditLeaveType – POST – SQL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditLeaveType(LeaveType leaveType)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            ModelState.Remove("LeaveRequests");

            if (!ModelState.IsValid)
            {
                return View(leaveType);
            }

            try
            {
                const string sql = @"
                    UPDATE LeaveTypes
                    SET TypeName = {0}, DaysAllowed = {1}
                    WHERE LeaveTypeID = {2}";

                _context.Database.ExecuteSqlRaw(sql, leaveType.TypeName, leaveType.DaysAllowed, leaveType.LeaveTypeId);

                TempData["SuccessMessage"] = "İzin türü başarıyla güncellendi!";
                return RedirectToAction("LeaveTypes");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(leaveType);
            }
        }

        // DeleteLeaveType – SQL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteLeaveType(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            const string sqlCheck = "SELECT COUNT(*) AS Value FROM LeaveRequests WHERE LeaveTypeID = {0}";
            var hasRequests = _context.Database.SqlQueryRaw<int>(sqlCheck, id).AsEnumerable().FirstOrDefault();

            if (hasRequests > 0)
            {
                TempData["ErrorMessage"] = "Bu izin türüne bağlı izin talepleri var! Silinemez.";
                return RedirectToAction("LeaveTypes");
            }

            try
            {
                const string sql = "DELETE FROM LeaveTypes WHERE LeaveTypeID = {0}";
                _context.Database.ExecuteSqlRaw(sql, id);

                TempData["SuccessMessage"] = "İzin türü başarıyla silindi!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Silme işlemi başarısız: " + ex.Message;
            }

            return RedirectToAction("LeaveTypes");
        }

        #endregion

        #region Locations Management

        // Locations – Listele – SQL
        public IActionResult Locations()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            const string sql = "SELECT * FROM Locations";
            var locations = _context.Locations.FromSqlRaw(sql).ToList();
            return View(locations);
        }

        [HttpGet]
        public IActionResult CreateLocation()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            return View();
        }

        // CreateLocation – SQL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateLocation(Location location)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            ModelState.Remove("Departments");

            if (!ModelState.IsValid)
            {
                return View(location);
            }

            try
            {
                const string sql = @"
                    INSERT INTO Locations (LocationName, Address)
                    VALUES ({0}, {1})";

                _context.Database.ExecuteSqlRaw(sql, location.LocationName, location.Address);

                TempData["SuccessMessage"] = "Lokasyon başarıyla eklendi!";
                return RedirectToAction("Locations");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(location);
            }
        }

        // EditLocation – GET – SQL
        [HttpGet]
        public IActionResult EditLocation(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            const string sql = "SELECT * FROM Locations WHERE LocationID = {0}";
            var location = _context.Locations.FromSqlRaw(sql, id).AsEnumerable().FirstOrDefault();

            if (location == null)
            {
                TempData["ErrorMessage"] = "Lokasyon bulunamadı!";
                return RedirectToAction("Locations");
            }

            return View(location);
        }

        // EditLocation – POST – SQL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditLocation(Location location)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            ModelState.Remove("Departments");

            if (!ModelState.IsValid)
            {
                return View(location);
            }

            try
            {
                const string sql = @"
                    UPDATE Locations
                    SET LocationName = {0}, Address = {1}
                    WHERE LocationID = {2}";

                _context.Database.ExecuteSqlRaw(sql, location.LocationName, location.Address, location.LocationId);

                TempData["SuccessMessage"] = "Lokasyon başarıyla güncellendi!";
                return RedirectToAction("Locations");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(location);
            }
        }

        // DeleteLocation – SQL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteLocation(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            const string sqlCheck = "SELECT COUNT(*) AS Value FROM Departments WHERE LocationID = {0}";
            var hasDepartments = _context.Database.SqlQueryRaw<int>(sqlCheck, id).AsEnumerable().FirstOrDefault();

            if (hasDepartments > 0)
            {
                TempData["ErrorMessage"] = "Bu lokasyona bağlı departmanlar var! Önce departmanların lokasyonunu değiştirin.";
                return RedirectToAction("Locations");
            }

            try
            {
                const string sql = "DELETE FROM Locations WHERE LocationID = {0}";
                _context.Database.ExecuteSqlRaw(sql, id);

                TempData["SuccessMessage"] = "Lokasyon başarıyla silindi!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Silme işlemi başarısız: " + ex.Message;
            }

            return RedirectToAction("Locations");
        }

        #endregion

        #region Roles Management

        // Roles – Listele – SQL
        public IActionResult Roles()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            const string sql = "SELECT * FROM Roles";
            var roles = _context.Roles.FromSqlRaw(sql).ToList();
            return View(roles);
        }

        [HttpGet]
        public IActionResult CreateRole()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            return View();
        }

        // Role Ekle – SQL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateRole(Role role)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            ModelState.Remove("UserRoles");

            if (!ModelState.IsValid)
            {
                return View(role);
            }

            const string sqlCheck = "SELECT COUNT(*) AS Value FROM Roles WHERE RoleName = {0}";
            var exists = _context.Database.SqlQueryRaw<int>(sqlCheck, role.RoleName).AsEnumerable().FirstOrDefault();

            if (exists > 0)
            {
                ModelState.AddModelError("", "Bu rol adı zaten mevcut!");
                return View(role);
            }

            try
            {
                const string sql = @"
                    INSERT INTO Roles (RoleName, RoleDescription)
                    VALUES ({0}, {1})";

                _context.Database.ExecuteSqlRaw(sql, role.RoleName, role.RoleDescription);

                TempData["SuccessMessage"] = "Rol başarıyla eklendi!";
                return RedirectToAction("Roles");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(role);
            }
        }

        #endregion
    }
}