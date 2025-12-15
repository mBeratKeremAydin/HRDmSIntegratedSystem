using HRDms.Data.Context;
using HRDms.Data.Models;
using HR.Mvc.Models;
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

            // 1. Basic Info
            const string sqlInfo = @"
                SELECT e.EmployeeID, e.FirstName, e.LastName, j.JobTitle, d.DepartmentName
                FROM Employees e
                LEFT JOIN Jobs j ON e.JobID = j.JobID
                LEFT JOIN Departments d ON e.DepartmentID = d.DepartmentID
                WHERE e.EmployeeID = {0}";

            var basicInfo = _context.Database.SqlQueryRaw<EmployeeBasicInfoDTO>(sqlInfo, employeeId.Value)
                .AsEnumerable()
                .FirstOrDefault();

            if (basicInfo == null)
            {
                TempData["ErrorMessage"] = "Çalışan bilgisi bulunamadı!";
                return RedirectToAction("Index", "Login");
            }

            var dashboardModel = new EmployeeDashboardViewModel
            {
                EmployeeId = basicInfo.EmployeeId,
                FirstName = basicInfo.FirstName,
                LastName = basicInfo.LastName,
                JobTitle = basicInfo.JobTitle,
                DepartmentName = basicInfo.DepartmentName
            };

            // 2. Counts
            dashboardModel.TotalLeaveRequests = _context.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM LeaveRequests WHERE EmployeeID = {0}", employeeId.Value).AsEnumerable().First();
            dashboardModel.PendingLeaveRequests = _context.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM LeaveRequests WHERE EmployeeID = {0} AND Status = 'Pending'", employeeId.Value).AsEnumerable().First();
            dashboardModel.ApprovedLeaveRequests = _context.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM LeaveRequests WHERE EmployeeID = {0} AND Status = 'Approved'", employeeId.Value).AsEnumerable().First();
            dashboardModel.TotalAttendanceDays = _context.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM Attendances WHERE EmployeeID = {0}", employeeId.Value).AsEnumerable().First();
            dashboardModel.TotalDocuments = _context.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM Documents WHERE OwnerEmployeeId = {0}", employeeId.Value).AsEnumerable().First();
            dashboardModel.PerformanceReviews = _context.Database.SqlQueryRaw<int>("SELECT COUNT(*) FROM PerformanceReviews WHERE EmployeeID = {0}", employeeId.Value).AsEnumerable().First();

            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;
            dashboardModel.CurrentMonthAttendance = _context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) FROM Attendances WHERE EmployeeID = {0} AND MONTH(Date) = {1} AND YEAR(Date) = {2}", 
                employeeId.Value, currentMonth, currentYear).AsEnumerable().First();

            // 3. Recent Leave Requests
            var sqlLeaves = @"
                SELECT TOP 5 lt.TypeName, lr.StartDate, lr.EndDate, lr.Status, lr.Reason
                FROM LeaveRequests lr
                LEFT JOIN LeaveTypes lt ON lr.LeaveTypeID = lt.LeaveTypeID
                WHERE lr.EmployeeID = {0}
                ORDER BY lr.StartDate DESC";

            dashboardModel.RecentLeaveRequests = _context.Database.SqlQueryRaw<DashboardLeaveRequestViewModel>(sqlLeaves, employeeId.Value).ToList();

            // ViewBags for compatibility (optional, but View will be updated)
            ViewBag.TotalLeaveRequests = dashboardModel.TotalLeaveRequests;
            ViewBag.PendingLeaveRequests = dashboardModel.PendingLeaveRequests;
            ViewBag.CurrentMonthAttendance = dashboardModel.CurrentMonthAttendance;
            ViewBag.TotalDocuments = dashboardModel.TotalDocuments;

            return View(dashboardModel);
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

            const string sql = @"
                SELECT 
                    e.EmployeeID, e.FirstName, e.LastName, e.Email, e.PhoneNumber, e.IdentityNumber, e.IsActive, e.HireDate,
                    j.JobTitle,
                    d.DepartmentName,
                    m.FirstName AS ManagerFirstName, m.LastName AS ManagerLastName, m.Email AS ManagerEmail,
                    mj.JobTitle AS ManagerJobTitle
                FROM Employees e
                LEFT JOIN Jobs j ON e.JobID = j.JobID
                LEFT JOIN Departments d ON e.DepartmentID = d.DepartmentID
                LEFT JOIN Employees m ON e.ManagerID = m.EmployeeID
                LEFT JOIN Jobs mj ON m.JobID = mj.JobID
                WHERE e.EmployeeID = {0}";

            var empDto = _context.Database.SqlQueryRaw<EmployeeDetailsDto>(sql, id)
                .AsEnumerable()
                .FirstOrDefault();

            if (empDto == null)
            {
                return NotFound();
            }

            var empDetails = new EmployeeDetailsViewModel
            {
                EmployeeId = empDto.EmployeeId,
                FirstName = empDto.FirstName,
                LastName = empDto.LastName,
                Email = empDto.Email,
                PhoneNumber = empDto.PhoneNumber,
                IdentityNumber = empDto.IdentityNumber,
                IsActive = empDto.IsActive,
                HireDate = empDto.HireDate,
                JobTitle = empDto.JobTitle,
                DepartmentName = empDto.DepartmentName,
                ManagerFirstName = empDto.ManagerFirstName,
                ManagerLastName = empDto.ManagerLastName,
                ManagerEmail = empDto.ManagerEmail,
                ManagerJobTitle = empDto.ManagerJobTitle
            };

            // Active Contract
            const string sqlContract = @"
                SELECT TOP 1 ContractType, StartDate, EndDate, Salary
                FROM EmploymentContracts
                WHERE EmployeeID = {0} AND IsActive = 1";
            
            var contract = _context.Database.SqlQueryRaw<EmploymentContractViewModel>(sqlContract, id)
                .AsEnumerable()
                .FirstOrDefault();
            
            empDetails.ActiveContract = contract;

            return View(empDetails);
        }

        // GET: Employee/Create
        [HttpGet]
        public IActionResult Create()
        {
            // Departments
            const string sqlDeps = @"SELECT DepartmentID, DepartmentName FROM Departments";
            ViewBag.Departments = _context.Database.SqlQueryRaw<DepartmentDTO>(sqlDeps).ToList();

            // Jobs
            const string sqlJobs = @"SELECT JobID, JobTitle FROM Jobs";
            ViewBag.Jobs = _context.Database.SqlQueryRaw<JobDTO>(sqlJobs).ToList();

            // Managers: HR veya Admin rolüne sahip kullanıcılar
            const string sqlManagers = @"
                SELECT u.UserID, u.Username
                FROM Users u
                JOIN UserRoles ur ON u.UserID = ur.UserID
                JOIN Roles r ON ur.RoleID = r.RoleID
                WHERE r.RoleName IN ('HR', 'Admin')";

            var managers = _context.Database.SqlQueryRaw<UserDTO>(sqlManagers)
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

                var mgrUserId = HttpContext.Session.GetInt32("UserId");

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
                        mgrUserId,
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
            const string sqlDeps = @"SELECT DepartmentID, DepartmentName FROM Departments";
            ViewBag.Departments = _context.Database.SqlQueryRaw<DepartmentDTO>(sqlDeps).ToList();

            // Jobs
            const string sqlJobs = @"SELECT JobID, JobTitle FROM Jobs";
            ViewBag.Jobs = _context.Database.SqlQueryRaw<JobDTO>(sqlJobs).ToList();

            // Managers
            const string sqlManagers = @"
                SELECT u.UserID, u.Username
                FROM Users u
                JOIN UserRoles ur ON u.UserID = ur.UserID
                JOIN Roles r ON ur.RoleID = r.RoleID
                WHERE r.RoleName IN ('HR', 'Admin')";

            ViewBag.Managers = _context.Database.SqlQueryRaw<UserDTO>(sqlManagers)
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

            const string sqlEmp = @"SELECT EmployeeID, UserID FROM Employees WHERE EmployeeID = {0}";
            var emp = _context.Database.SqlQueryRaw<EmployeeIdUserIdDTO>(sqlEmp, id)
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

            const string sqlDeps = @"SELECT DepartmentID, DepartmentName FROM Departments";
            ViewBag.Departments = _context.Database.SqlQueryRaw<DepartmentDTO>(sqlDeps)
                .AsEnumerable()
                .Select(d => new SelectListItem
                {
                    Value = d.DepartmentId.ToString(),
                    Text = d.DepartmentName,
                    Selected = d.DepartmentId == emp.DepartmentId
                })
                .ToList();

            const string sqlJobs = @"SELECT JobID, JobTitle FROM Jobs";
            ViewBag.Jobs = _context.Database.SqlQueryRaw<JobDTO>(sqlJobs)
                .AsEnumerable()
                .Select(j => new SelectListItem
                {
                    Value = j.JobId.ToString(),
                    Text = j.JobTitle,
                    Selected = j.JobId == emp.JobId
                })
                .ToList();

            const string sqlManagers = @"
                SELECT u.UserID, u.Username
                FROM Users u
                JOIN UserRoles ur ON u.UserID = ur.UserID
                JOIN Roles r ON ur.RoleID = r.RoleID
                WHERE r.RoleName IN ('HR', 'Admin')";

            ViewBag.Managers = _context.Database.SqlQueryRaw<UserDTO>(sqlManagers)
                .AsEnumerable()
                .DistinctBy(u => u.UserId)
                .Select(u => new SelectListItem
                {
                    Value = u.UserId.ToString(),
                    Text = u.Username,
                    Selected = (emp.ManagerId != null && u.UserId == emp.ManagerId)
                })
                .ToList();

            const string sqlContract = @"SELECT * FROM EmploymentContracts WHERE EmployeeID = {0} AND IsActive = 1";
            var activeContract = _context.EmploymentContracts
                .FromSqlRaw(sqlContract, id)
                .AsEnumerable()
                .FirstOrDefault();
            
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
                    .AsEnumerable()
                    .FirstOrDefault();

                const string sqlContractReload = @"SELECT * FROM EmploymentContracts WHERE EmployeeID = {0} AND IsActive = 1";
                ViewBag.ActiveContract = _context.EmploymentContracts
                    .FromSqlRaw(sqlContractReload, model.EmployeeId)
                    .AsEnumerable()
                    .FirstOrDefault();

                return View(model);
            }

            const string sqlEmp = @"SELECT * FROM Employees WHERE EmployeeID = {0}";
            var employee = _context.Employees
                .FromSqlRaw(sqlEmp, model.EmployeeId)
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
            int deptId = employee.DepartmentId;
            if (!isDepartmentManager)
            {
                deptId = model.DepartmentId;
            }

            const string sqlUpdateEmp = @"
                UPDATE Employees SET 
                    FirstName = {0}, LastName = {1}, Email = {2}, PhoneNumber = {3}, 
                    IdentityNumber = {4}, HireDate = {5}, JobID = {6}, ManagerID = {7}, 
                    IsActive = {8}, DepartmentID = {9}
                WHERE EmployeeID = {10}";

            _context.Database.ExecuteSqlRaw(sqlUpdateEmp, 
                model.FirstName, model.LastName, model.Email, model.PhoneNumber, 
                model.IdentityNumber, model.HireDate, model.JobId, model.ManagerId, 
                model.IsActive, deptId, model.EmployeeId);

            // Sözleşme güncellemesi
            if (updateContract && contractId.HasValue && contractStartDate.HasValue && contractSalary.HasValue)
            {
                const string sqlUpdateContract = @"
                    UPDATE EmploymentContracts SET
                        StartDate = {0}, EndDate = {1}, Salary = {2}, ContractType = {3}
                    WHERE ContractId = {4}";
                
                _context.Database.ExecuteSqlRaw(sqlUpdateContract,
                    contractStartDate.Value, contractEndDate, contractSalary.Value, contractType ?? "Belirsiz Süreli", contractId.Value);
            }

            TempData["SuccessMessage"] = "Çalışan ve sözleşme bilgileri başarıyla güncellendi!";
            return RedirectToAction("Index", "HR");
        }

        private void ReloadDropdownsForEdit(Employee model)
        {
            const string sqlDeps = @"SELECT DepartmentID, DepartmentName FROM Departments";
            ViewBag.Departments = _context.Database.SqlQueryRaw<DepartmentDTO>(sqlDeps)
                .AsEnumerable()
                .Select(d => new SelectListItem
                {
                    Value = d.DepartmentId.ToString(),
                    Text = d.DepartmentName,
                    Selected = d.DepartmentId == model.DepartmentId
                })
                .ToList();

            const string sqlJobs = @"SELECT JobID, JobTitle FROM Jobs";
            ViewBag.Jobs = _context.Database.SqlQueryRaw<JobDTO>(sqlJobs)
                .AsEnumerable()
                .Select(j => new SelectListItem
                {
                    Value = j.JobId.ToString(),
                    Text = j.JobTitle,
                    Selected = j.JobId == model.JobId
                })
                .ToList();

            const string sqlManagers = @"
                SELECT u.UserID, u.Username
                FROM Users u
                JOIN UserRoles ur ON u.UserID = ur.UserID
                JOIN Roles r ON ur.RoleID = r.RoleID
                WHERE r.RoleName IN ('HR', 'Admin')";

            ViewBag.Managers = _context.Database.SqlQueryRaw<UserDTO>(sqlManagers)
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

            const string sqlInfo = @"
                SELECT e.EmployeeID, e.FirstName, e.LastName, j.JobTitle, d.DepartmentName
                FROM Employees e
                LEFT JOIN Jobs j ON e.JobID = j.JobID
                LEFT JOIN Departments d ON e.DepartmentID = d.DepartmentID
                WHERE e.EmployeeID = {0}";

            var basicInfo = _context.Database.SqlQueryRaw<EmployeeBasicInfoDTO>(sqlInfo, id)
                .AsEnumerable()
                .FirstOrDefault();

            if (basicInfo == null)
                return NotFound();

            var model = new EmployeeAttendanceViewModel
            {
                EmployeeId = basicInfo.EmployeeId,
                FirstName = basicInfo.FirstName,
                LastName = basicInfo.LastName,
                JobTitle = basicInfo.JobTitle,
                DepartmentName = basicInfo.DepartmentName
            };

            const string sqlAttendances = @"
                SELECT AttendanceID, Date, CheckInTime, CheckOutTime
                FROM Attendances
                WHERE EmployeeID = {0}
                ORDER BY Date DESC";
            
            var attendanceDtos = _context.Database.SqlQueryRaw<AttendanceViewModel>(sqlAttendances, id).ToList();
            model.Attendances = attendanceDtos;

            return View(model);
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
            const string sqlInfo = @"
                SELECT e.EmployeeID, e.FirstName, e.LastName, j.JobTitle, d.DepartmentName
                FROM Employees e
                LEFT JOIN Jobs j ON e.JobID = j.JobID
                LEFT JOIN Departments d ON e.DepartmentID = d.DepartmentID
                WHERE e.EmployeeID = {0}";

            var basicInfo = _context.Database.SqlQueryRaw<EmployeeBasicInfoDTO>(sqlInfo, id)
                .AsEnumerable()
                .FirstOrDefault();

            if (basicInfo == null) return NotFound();

            var model = new EmployeeLeavesViewModel
            {
                EmployeeId = basicInfo.EmployeeId,
                FirstName = basicInfo.FirstName,
                LastName = basicInfo.LastName,
                JobTitle = basicInfo.JobTitle,
                DepartmentName = basicInfo.DepartmentName
            };

            const string sqlLeaves = @"
                SELECT 
                    lr.RequestId, 
                    lr.EmployeeId, 
                    e.FirstName AS EmployeeFirstName,
                    e.LastName AS EmployeeLastName,
                    d.DepartmentName,
                    j.JobTitle,
                    e.Email,
                    lt.TypeName AS LeaveTypeName, 
                    lr.StartDate, 
                    lr.EndDate, 
                    lr.Status, 
                    lr.Reason,
                    lr.ApprovedByUserId,
                    u.Username AS ApprovedByUserName
                FROM LeaveRequests lr
                INNER JOIN Employees e ON lr.EmployeeId = e.EmployeeId
                LEFT JOIN Departments d ON e.DepartmentId = d.DepartmentId
                LEFT JOIN Jobs j ON e.JobId = j.JobId
                LEFT JOIN LeaveTypes lt ON lr.LeaveTypeID = lt.LeaveTypeID
                LEFT JOIN Users u ON lr.ApprovedByUserId = u.UserId
                WHERE lr.EmployeeID = {0}
                ORDER BY lr.StartDate DESC";

            model.LeaveRequests = _context.Database.SqlQueryRaw<LeaveRequestViewModel>(sqlLeaves, id).ToList();

            return View(model);
        }

        public IActionResult Performance(int id)
        {
            const string sqlInfo = @"
                SELECT e.EmployeeID, e.FirstName, e.LastName, j.JobTitle, d.DepartmentName
                FROM Employees e
                LEFT JOIN Jobs j ON e.JobID = j.JobID
                LEFT JOIN Departments d ON e.DepartmentID = d.DepartmentID
                WHERE e.EmployeeID = {0}";

            var basicInfo = _context.Database.SqlQueryRaw<EmployeeBasicInfoDTO>(sqlInfo, id)
                .AsEnumerable()
                .FirstOrDefault();

            if (basicInfo == null) return NotFound();

            var model = new EmployeePerformanceViewModel
            {
                EmployeeId = basicInfo.EmployeeId,
                FirstName = basicInfo.FirstName,
                LastName = basicInfo.LastName,
                JobTitle = basicInfo.JobTitle,
                DepartmentName = basicInfo.DepartmentName
            };

            const string sqlReviews = @"
                SELECT pr.ReviewId, pr.EmployeeId, pr.ReviewDate, pr.Score, pr.Notes, 
                       r.FirstName + ' ' + r.LastName AS ReviewerName
                FROM PerformanceReviews pr
                LEFT JOIN Employees r ON pr.ReviewerID = r.EmployeeID
                WHERE pr.EmployeeID = {0}
                ORDER BY pr.ReviewDate DESC";

            model.Reviews = _context.Database.SqlQueryRaw<PerformanceReviewViewModel>(sqlReviews, id).ToList();

            return View(model);
        }

        public IActionResult Documents(int id)
        {
            const string sqlInfo = @"
                SELECT e.EmployeeID, e.FirstName, e.LastName, j.JobTitle, d.DepartmentName
                FROM Employees e
                LEFT JOIN Jobs j ON e.JobID = j.JobID
                LEFT JOIN Departments d ON e.DepartmentID = d.DepartmentID
                WHERE e.EmployeeID = {0}";

            var basicInfo = _context.Database.SqlQueryRaw<EmployeeBasicInfoDTO>(sqlInfo, id)
                .AsEnumerable()
                .FirstOrDefault();

            if (basicInfo == null) return NotFound();

            var model = new EmployeeDocumentsViewModel
            {
                EmployeeId = basicInfo.EmployeeId,
                FirstName = basicInfo.FirstName,
                LastName = basicInfo.LastName,
                JobTitle = basicInfo.JobTitle,
                DepartmentName = basicInfo.DepartmentName
            };

            const string sqlDocs = @"
                SELECT 
                    d.DocumentId, 
                    d.Title, 
                    d.DocumentDescription AS Description, 
                    d.CreatedDate, 
                    d.CurrentStatus AS Status, 
                    c.CategoryName,
                    d.IsActive
                FROM Documents d
                LEFT JOIN DocumentCategories c ON d.CategoryID = c.CategoryID
                WHERE d.OwnerEmployeeId = {0}
                ORDER BY d.CreatedDate DESC";

            model.Documents = _context.Database.SqlQueryRaw<DocumentViewModel>(sqlDocs, id).ToList();

            return View(model);
        }
    }
}