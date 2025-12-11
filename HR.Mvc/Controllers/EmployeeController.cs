using HRDms.Data.Context;
using HRDms.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HR.Mvc.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly AppDbContext _context;
        public EmployeeController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // Session kontrolü
            var userId = HttpContext.Session.GetInt32("UserId");
            var employeeId = HttpContext.Session.GetInt32("EmployeeId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (employeeId == null)
            {
                TempData["ErrorMessage"] = "Oturumdaki EmployeeId bulunamadı!";
                return RedirectToAction("Index", "Login");
            }

            // Employee bilgisini getir (SQL + Include)
            const string sqlEmp = @"SELECT * FROM Employees WHERE EmployeeID = {0}";
            var employee = _context.Employees
                .FromSqlRaw(sqlEmp, employeeId.Value)
                .Include(e => e.Department)
                .Include(e => e.Job)
                .Include(e => e.Manager)
                .Include(e => e.EmploymentContracts.Where(c => c.IsActive))
                .Include(e => e.LeaveRequests)
                    .ThenInclude(l => l.LeaveType)
                .Include(e => e.Attendances)
                .Include(e => e.PerformanceReviewEmployees)
                .Include(e => e.Documents)
                .AsEnumerable()
                .FirstOrDefault();

            if (employee == null)
            {
                TempData["ErrorMessage"] = "Çalışan bilgisi bulunamadı!";
                return RedirectToAction("Index", "Login");
            }

            // İstatistikler için hesaplamalar (in-memory)
            ViewBag.TotalLeaveRequests = employee.LeaveRequests.Count;
            ViewBag.PendingLeaveRequests = employee.LeaveRequests.Count(l => l.Status == "Pending");
            ViewBag.ApprovedLeaveRequests = employee.LeaveRequests.Count(l => l.Status == "Approved");
            ViewBag.TotalAttendanceDays = employee.Attendances.Count;
            ViewBag.TotalDocuments = employee.Documents.Count;
            ViewBag.PerformanceReviews = employee.PerformanceReviewEmployees.Count;

            // Son izin talebi
            ViewBag.LastLeaveRequest = employee.LeaveRequests
                .OrderByDescending(l => l.StartDate)
                .FirstOrDefault();

            // Bu ay devamsızlık
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;
            ViewBag.CurrentMonthAttendance = employee.Attendances
                .Count(a => a.Date.HasValue && a.Date.Value.Month == currentMonth && a.Date.Value.Year == currentYear);

            return View(employee);
        }

        public IActionResult Details(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var sessionEmployeeId = HttpContext.Session.GetInt32("EmployeeId");

            // Employee ise sadece kendi profilini görebilir
            if (userRole == "Employee" && sessionEmployeeId != id)
            {
                TempData["ErrorMessage"] = "Başka çalışanların profillerini görüntüleme yetkiniz yok!";
                return RedirectToAction("Index");
            }

            const string sql = @"SELECT * FROM Employees WHERE EmployeeID = {0}";
            var emp = _context.Employees
                .FromSqlRaw(sql, id)
                .Include(e => e.Department)
                    .ThenInclude(d => d.Manager)
                .Include(e => e.Job)
                .Include(e => e.Manager)
                .Include(e => e.EmploymentContracts)
                .AsEnumerable()
                .FirstOrDefault();

            if (emp == null)
                return NotFound();

            return View(emp);
        }

        // GET: Employee/Create
        [HttpGet]
        public IActionResult Create()
        {
            // Departments
            const string sqlDeps = @"SELECT * FROM Departments";
            ViewBag.Departments = _context.Departments
                .FromSqlRaw(sqlDeps)
                .AsEnumerable()
                .Select(d => new { d.DepartmentId, d.DepartmentName })
                .ToList();

            // Jobs
            const string sqlJobs = @"SELECT * FROM Jobs";
            ViewBag.Jobs = _context.Jobs
                .FromSqlRaw(sqlJobs)
                .AsEnumerable()
                .Select(j => new { j.JobId, j.JobTitle })
                .ToList();

            // Managers: HR veya Admin rolüne sahip kullanıcılar
            const string sqlManagers = @"
                SELECT u.*
                FROM Users u
                JOIN UserRoles ur ON u.UserID = ur.UserID
                JOIN Roles r ON ur.RoleID = r.RoleID
                WHERE r.RoleName IN ('HR', 'Admin')";

            var managers = _context.Users
                .FromSqlRaw(sqlManagers)
                .AsEnumerable()
                .DistinctBy(u => u.UserId) // aynı kullanıcıya birden fazla rol gelirse
                .Select(u => new SelectListItem
                {
                    Value = u.UserId.ToString(),
                    Text = u.Username
                })
                .ToList();

            ViewBag.Managers = managers;

            return View();
        }

        // POST: Employee/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(
            Employee employee,
            bool createUser = false,
            string? username = null,
            string? userPassword = null,
            bool createContract = false,
            DateOnly? contractStartDate = null,
            DateOnly? contractEndDate = null,
            decimal? contractSalary = null,
            string? contractType = null)
        {
            // Rol id'leri (SQL)
            const string sqlHrRole = "SELECT RoleID FROM Roles WHERE RoleName = 'HR'";
            int hrRoleId = _context.Database
                .SqlQueryRaw<int>(sqlHrRole)
                .AsEnumerable()
                .FirstOrDefault();

            const string sqlEmpRole = "SELECT RoleID FROM Roles WHERE RoleName = 'Employee'";
            int employeeRoleId = _context.Database
                .SqlQueryRaw<int>(sqlEmpRole)
                .AsEnumerable()
                .FirstOrDefault();

            try
            {
                // ZORUNLU: User oluşturulmalı
                if (!createUser || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(userPassword))
                {
                    ModelState.AddModelError("", "Her çalışan için kullanıcı hesabı oluşturulmalıdır!");
                    ReloadDropdowns();
                    return View(employee);
                }

                // Kullanıcı adı kontrolü (SQL)
                const string sqlCheckUsername = "SELECT COUNT(*) AS Value FROM Users WHERE Username = {0}";
                var usernameExists = _context.Database
                    .SqlQueryRaw<int>(sqlCheckUsername, username)
                    .AsEnumerable()
                    .FirstOrDefault() > 0;

                if (usernameExists)
                {
                    ModelState.AddModelError("Username", "Bu kullanıcı adı zaten kullanılıyor.");
                    ReloadDropdowns();
                    return View(employee);
                }

                // EMAIL KONTROLÜ (SQL)
                if (!string.IsNullOrEmpty(employee.Email))
                {
                    const string sqlCheckEmail = "SELECT COUNT(*) AS Value FROM Users WHERE Email = {0}";
                    var emailExists = _context.Database
                        .SqlQueryRaw<int>(sqlCheckEmail, employee.Email)
                        .AsEnumerable()
                        .FirstOrDefault() > 0;

                    if (emailExists)
                    {
                        ModelState.AddModelError("Email", "Bu email adresi zaten kullanılıyor.");
                        ReloadDropdowns();
                        return View(employee);
                    }
                }

                // User INSERT
                const string sqlInsertUser = @"
                    INSERT INTO Users (Username, UserPassword, Email, IsActive)
                    VALUES ({0}, {1}, {2}, {3});
                    SELECT CAST(SCOPE_IDENTITY() AS int);";

                int newUserId = _context.Database
                    .SqlQueryRaw<int>(sqlInsertUser, username, userPassword, employee.Email, employee.IsActive)
                    .AsEnumerable()
                    .First();

                employee.UserId = newUserId;

                // Departman kontrolü ve HR rolü atama
                const string sqlDep = @"SELECT * FROM Departments WHERE DepartmentID = {0}";
                var department = _context.Departments
                    .FromSqlRaw(sqlDep, employee.DepartmentId)
                    .AsEnumerable()
                    .FirstOrDefault();

                if (department != null &&
                    !string.IsNullOrEmpty(department.DepartmentName) &&
                    department.DepartmentName.Equals("HR", StringComparison.OrdinalIgnoreCase) &&
                    hrRoleId != 0)
                {
                    const string sqlInsertHrUserRole = @"
                        INSERT INTO UserRoles (UserID, RoleID, AssignedDate)
                        VALUES ({0}, {1}, GETDATE())";

                    _context.Database.ExecuteSqlRaw(sqlInsertHrUserRole, newUserId, hrRoleId);
                }

                // Her çalışana Employee rolü
                if (employeeRoleId != 0)
                {
                    const string sqlInsertEmpUserRole = @"
                        INSERT INTO UserRoles (UserID, RoleID, AssignedDate)
                        VALUES ({0}, {1}, GETDATE())";

                    _context.Database.ExecuteSqlRaw(sqlInsertEmpUserRole, newUserId, employeeRoleId);
                }

                // Employee INSERT
                const string sqlInsertEmp = @"
                    INSERT INTO Employees 
                        (FirstName, LastName, Email, PhoneNumber, IdentityNumber, HireDate, 
                         DepartmentID, JobID, ManagerID, UserID, IsActive)
                    VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10});
                    SELECT CAST(SCOPE_IDENTITY() AS int);";

                int newEmployeeId = _context.Database
                    .SqlQueryRaw<int>(
                        sqlInsertEmp,
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

                employee.EmployeeId = newEmployeeId;

                // Sözleşme oluşturma
                if (createContract && contractStartDate.HasValue && contractSalary.HasValue)
                {
                    const string sqlInsertContract = @"
                        INSERT INTO EmploymentContracts
                            (EmployeeID, StartDate, EndDate, Salary, ContractType, IsActive)
                        VALUES ({0}, {1}, {2}, {3}, {4}, 1)";

                    _context.Database.ExecuteSqlRaw(
                        sqlInsertContract,
                        newEmployeeId,
                        contractStartDate.Value,
                        contractEndDate,
                        contractSalary.Value,
                        contractType ?? "Belirsiz Süreli");
                }

                TempData["SuccessMessage"] = "Çalışan başarıyla eklendi!";
                return RedirectToAction("Index", "HR");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                ReloadDropdowns();
                return View(employee);
            }
        }

        private void ReloadDropdowns()
        {
            // Departments
            const string sqlDeps = @"SELECT * FROM Departments";
            ViewBag.Departments = _context.Departments
                .FromSqlRaw(sqlDeps)
                .AsEnumerable()
                .Select(d => new { d.DepartmentId, d.DepartmentName })
                .ToList();

            // Jobs
            const string sqlJobs = @"SELECT * FROM Jobs";
            ViewBag.Jobs = _context.Jobs
                .FromSqlRaw(sqlJobs)
                .AsEnumerable()
                .Select(j => new { j.JobId, j.JobTitle })
                .ToList();

            // Managers
            const string sqlManagers = @"
                SELECT u.*
                FROM Users u
                JOIN UserRoles ur ON u.UserID = ur.UserID
                JOIN Roles r ON ur.RoleID = r.RoleID
                WHERE r.RoleName IN ('HR', 'Admin')";

            ViewBag.Managers = _context.Users
                .FromSqlRaw(sqlManagers)
                .AsEnumerable()
                .DistinctBy(u => u.UserId)
                .Select(u => new SelectListItem
                {
                    Value = u.UserId.ToString(),
                    Text = u.Username
                })
                .ToList();
        }

        public IActionResult Delete(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var employeeId = HttpContext.Session.GetInt32("EmployeeId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (id == employeeId || userId == id)
            {
                TempData["ErrorMessage"] = "Kendi profilinizi silemezsiniz!";
                return RedirectToAction("Index", "HR");
            }

            const string sqlEmp = @"SELECT * FROM Employees WHERE EmployeeID = {0}";
            var emp = _context.Employees
                .FromSqlRaw(sqlEmp, id)
                .AsEnumerable()
                .FirstOrDefault();

            if (emp == null)
            {
                TempData["ErrorMessage"] = "Çalışan bulunamadı!";
                return RedirectToAction("Index", "HR");
            }

            // 1) Manager ise, astların ManagerID'sini NULL yap
            const string sqlNullSubs = @"UPDATE Employees SET ManagerID = NULL WHERE ManagerID = {0}";
            _context.Database.ExecuteSqlRaw(sqlNullSubs, id);

            // 2) Reviewer ise, PerformanceReview.ReviewerID NULL yapılmalı
            const string sqlNullReviews = @"UPDATE PerformanceReviews SET ReviewerID = NULL WHERE ReviewerID = {0}";
            _context.Database.ExecuteSqlRaw(sqlNullReviews, id);

            // 3) Employee'yi sil
            const string sqlDeleteEmp = @"DELETE FROM Employees WHERE EmployeeID = {0}";
            _context.Database.ExecuteSqlRaw(sqlDeleteEmp, id);

            // 4) Kullanıcıyı da sil
            if (emp.UserId != null)
            {
                const string sqlDeleteUser = @"DELETE FROM Users WHERE UserID = {0}";
                _context.Database.ExecuteSqlRaw(sqlDeleteUser, emp.UserId.Value);
            }

            return RedirectToAction("Index", "HR");
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var employeeId = HttpContext.Session.GetInt32("EmployeeId");
            var sessionUserId = HttpContext.Session.GetInt32("UserId");

            if (id == employeeId || sessionUserId == id)
            {
                TempData["ErrorMessage"] = "Kendi profilinizi Güncelleyemezsiniz!";
                return RedirectToAction("Index", "HR");
            }

            const string sqlEmp = @"SELECT * FROM Employees WHERE EmployeeID = {0}";
            var emp = _context.Employees
                .FromSqlRaw(sqlEmp, id)
                .Include(e => e.EmploymentContracts)
                .Include(e => e.Department)
                .AsEnumerable()
                .FirstOrDefault();

            if (emp == null)
                return NotFound();

            const string sqlIsMgr = @"SELECT COUNT(*) AS Value FROM Departments WHERE ManagerID = {0}";
            bool isDepartmentManager = _context.Database
                .SqlQueryRaw<int>(sqlIsMgr, emp.EmployeeId)
                .AsEnumerable()
                .FirstOrDefault() > 0;

            ViewBag.IsDepartmentManager = isDepartmentManager;

            const string sqlDeps = @"SELECT * FROM Departments";
            ViewBag.Departments = _context.Departments
                .FromSqlRaw(sqlDeps)
                .AsEnumerable()
                .Select(d => new SelectListItem
                {
                    Value = d.DepartmentId.ToString(),
                    Text = d.DepartmentName,
                    Selected = d.DepartmentId == emp.DepartmentId
                })
                .ToList();

            const string sqlJobs = @"SELECT * FROM Jobs";
            ViewBag.Jobs = _context.Jobs
                .FromSqlRaw(sqlJobs)
                .AsEnumerable()
                .Select(j => new SelectListItem
                {
                    Value = j.JobId.ToString(),
                    Text = j.JobTitle,
                    Selected = j.JobId == emp.JobId
                })
                .ToList();

            const string sqlManagers = @"
                SELECT u.*
                FROM Users u
                JOIN UserRoles ur ON u.UserID = ur.UserID
                JOIN Roles r ON ur.RoleID = r.RoleID
                WHERE r.RoleName IN ('HR', 'Admin')";

            ViewBag.Managers = _context.Users
                .FromSqlRaw(sqlManagers)
                .AsEnumerable()
                .DistinctBy(u => u.UserId)
                .Select(u => new SelectListItem
                {
                    Value = u.UserId.ToString(),
                    Text = u.Username,
                    Selected = (emp.ManagerId != null && u.UserId == emp.ManagerId)
                })
                .ToList();

            var activeContract = emp.EmploymentContracts?.FirstOrDefault(c => c.IsActive);
            ViewBag.ActiveContract = activeContract;

            return View(emp);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(
            Employee model,
            bool updateContract = false,
            int? contractId = null,
            DateOnly? contractStartDate = null,
            DateOnly? contractEndDate = null,
            decimal? contractSalary = null,
            string? contractType = null)
        {
            // Navigation property'leri ModelState'den temizle
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

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .Select(x => new
                    {
                        Field = x.Key,
                        Errors = x.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    })
                    .ToList();

                ViewBag.ValidationErrors = errors;
                ReloadDropdownsForEdit(model);

                const string sqlEmpReload = @"SELECT * FROM Employees WHERE EmployeeID = {0}";
                var empReload = _context.Employees
                    .FromSqlRaw(sqlEmpReload, model.EmployeeId)
                    .Include(e => e.EmploymentContracts)
                    .AsEnumerable()
                    .FirstOrDefault();

                ViewBag.ActiveContract = empReload?.EmploymentContracts?.FirstOrDefault(c => c.IsActive);

                return View(model);
            }

            const string sqlEmp = @"SELECT * FROM Employees WHERE EmployeeID = {0}";
            var employee = _context.Employees
                .FromSqlRaw(sqlEmp, model.EmployeeId)
                .Include(e => e.EmploymentContracts)
                .AsEnumerable()
                .FirstOrDefault();

            if (employee == null)
                return NotFound();

            const string sqlIsMgr = @"SELECT COUNT(*) AS Value FROM Departments WHERE ManagerID = {0}";
            bool isDepartmentManager = _context.Database
                .SqlQueryRaw<int>(sqlIsMgr, employee.EmployeeId)
                .AsEnumerable()
                .FirstOrDefault() > 0;

            // Çalışan bilgilerini güncelle
            employee.FirstName = model.FirstName;
            employee.LastName = model.LastName;
            employee.Email = model.Email;
            employee.PhoneNumber = model.PhoneNumber;
            employee.IdentityNumber = model.IdentityNumber;
            employee.HireDate = model.HireDate;
            employee.JobId = model.JobId;
            employee.ManagerId = model.ManagerId;
            employee.IsActive = model.IsActive;

            if (!isDepartmentManager)
            {
                employee.DepartmentId = model.DepartmentId;
            }

            // Sözleşme güncellemesi
            if (updateContract && contractId.HasValue && contractStartDate.HasValue && contractSalary.HasValue)
            {
                var contract = employee.EmploymentContracts.FirstOrDefault(c => c.ContractId == contractId.Value);

                if (contract != null)
                {
                    contract.StartDate = contractStartDate.Value;
                    contract.EndDate = contractEndDate;
                    contract.Salary = contractSalary.Value;
                    contract.ContractType = contractType ?? "Belirsiz Süreli";
                }
            }

            _context.SaveChanges();

            TempData["SuccessMessage"] = "Çalışan ve sözleşme bilgileri başarıyla güncellendi!";
            return RedirectToAction("Index", "HR");
        }

        private void ReloadDropdownsForEdit(Employee model)
        {
            const string sqlDeps = @"SELECT * FROM Departments";
            ViewBag.Departments = _context.Departments
                .FromSqlRaw(sqlDeps)
                .AsEnumerable()
                .Select(d => new SelectListItem
                {
                    Value = d.DepartmentId.ToString(),
                    Text = d.DepartmentName,
                    Selected = d.DepartmentId == model.DepartmentId
                })
                .ToList();

            const string sqlJobs = @"SELECT * FROM Jobs";
            ViewBag.Jobs = _context.Jobs
                .FromSqlRaw(sqlJobs)
                .AsEnumerable()
                .Select(j => new SelectListItem
                {
                    Value = j.JobId.ToString(),
                    Text = j.JobTitle,
                    Selected = j.JobId == model.JobId
                })
                .ToList();

            const string sqlManagers = @"
                SELECT u.*
                FROM Users u
                JOIN UserRoles ur ON u.UserID = ur.UserID
                JOIN Roles r ON ur.RoleID = r.RoleID
                WHERE r.RoleName IN ('HR', 'Admin')";

            ViewBag.Managers = _context.Users
                .FromSqlRaw(sqlManagers)
                .AsEnumerable()
                .DistinctBy(u => u.UserId)
                .Select(u => new SelectListItem
                {
                    Value = u.UserId.ToString(),
                    Text = u.Username,
                    Selected = (model.ManagerId != null && u.UserId == model.ManagerId)
                })
                .ToList();
        }

        // Devamsızlık görüntüleme ve ekleme
        [HttpGet]
        public IActionResult Attendance(int id)
        {
            var sessionEmployeeId = HttpContext.Session.GetInt32("EmployeeId");
            var userRole = HttpContext.Session.GetString("UserRole");

            // Employee ise sadece kendi kayıtlarını görebilir
            if (userRole == "Employee" && sessionEmployeeId != id)
            {
                TempData["ErrorMessage"] = "Başka çalışanların devamsızlık kayıtlarını görüntüleme yetkiniz yok!";
                return RedirectToAction("Index");
            }

            const string sqlEmp = @"SELECT * FROM Employees WHERE EmployeeID = {0}";
            var emp = _context.Employees
                .FromSqlRaw(sqlEmp, id)
                .Include(e => e.Department)
                .Include(e => e.Job)
                .Include(e => e.Attendances)
                .AsEnumerable()
                .FirstOrDefault();

            if (emp == null)
                return NotFound();

            return View(emp);
        }

        // Giriş kaydı ekleme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CheckIn(int employeeId)
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);

                const string sqlCheck = @"
                    SELECT COUNT(*) AS Value
                    FROM Attendances
                    WHERE EmployeeID = {0} AND [Date] = {1}";

                bool exists = _context.Database
                    .SqlQueryRaw<int>(sqlCheck, employeeId, today)
                    .AsEnumerable()
                    .FirstOrDefault() > 0;

                if (exists)
                {
                    TempData["ErrorMessage"] = "Bugün için zaten giriş kaydı mevcut!";
                    return RedirectToAction("Attendance", new { id = employeeId });
                }

                const string sqlInsert = @"
                    INSERT INTO Attendances (EmployeeID, [Date], CheckInTime, CheckOutTime)
                    VALUES ({0}, {1}, {2}, NULL)";

                _context.Database.ExecuteSqlRaw(sqlInsert, employeeId, today, DateTime.Now);

                TempData["SuccessMessage"] = "Giriş kaydı başarıyla oluşturuldu!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Bir hata oluştu: {ex.Message}";
            }

            return RedirectToAction("Attendance", new { id = employeeId });
        }

        // Çıkış kaydı güncelleme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CheckOut(int attendanceId, int employeeId)
        {
            try
            {
                const string sqlFind = @"SELECT * FROM Attendances WHERE AttendanceID = {0}";
                var attendance = _context.Attendances
                    .FromSqlRaw(sqlFind, attendanceId)
                    .AsEnumerable()
                    .FirstOrDefault();

                if (attendance == null)
                {
                    TempData["ErrorMessage"] = "Devamsızlık kaydı bulunamadı!";
                    return RedirectToAction("Attendance", new { id = employeeId });
                }

                if (attendance.CheckOutTime != null)
                {
                    TempData["ErrorMessage"] = "Bu kayıt için çıkış zaten yapılmış!";
                    return RedirectToAction("Attendance", new { id = employeeId });
                }

                const string sqlUpdate = @"
                    UPDATE Attendances
                    SET CheckOutTime = {0}
                    WHERE AttendanceID = {1}";

                _context.Database.ExecuteSqlRaw(sqlUpdate, DateTime.Now, attendanceId);

                TempData["SuccessMessage"] = "Çıkış kaydı başarıyla güncellendi!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Bir hata oluştu: {ex.Message}";
            }

            return RedirectToAction("Attendance", new { id = employeeId });
        }

        public IActionResult Leaves(int id)
        {
            const string sqlEmp = @"SELECT * FROM Employees WHERE EmployeeID = {0}";
            var emp = _context.Employees
                .FromSqlRaw(sqlEmp, id)
                .Include(e => e.LeaveRequests)
                    .ThenInclude(l => l.LeaveType)
                .AsEnumerable()
                .FirstOrDefault();

            if (emp == null) return NotFound();

            return View(emp);
        }

        public IActionResult Performance(int id)
        {
            const string sqlEmp = @"SELECT * FROM Employees WHERE EmployeeID = {0}";
            var emp = _context.Employees
                .FromSqlRaw(sqlEmp, id)
                .Include(e => e.PerformanceReviewEmployees)
                .AsEnumerable()
                .FirstOrDefault();

            if (emp == null) return NotFound();

            return View(emp);
        }

        public IActionResult Documents(int id)
        {
            const string sqlEmp = @"SELECT * FROM Employees WHERE EmployeeID = {0}";
            var emp = _context.Employees
                .FromSqlRaw(sqlEmp, id)
                .Include(e => e.Documents)
                .AsEnumerable()
                .FirstOrDefault();

            if (emp == null) return NotFound();

            return View(emp);
        }
    }
}