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

            // Employee bilgisini getir
            var employee = _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Job)
                .Include(e => e.Manager)
                .Include(e => e.EmploymentContracts.Where(c => c.IsActive))
                .Include(e => e.LeaveRequests)
                    .ThenInclude(l => l.LeaveType)
                .Include(e => e.Attendances)
                .Include(e => e.PerformanceReviewEmployees)
                .Include(e => e.Documents)
                .FirstOrDefault(e => e.EmployeeId == employeeId);

            if (employee == null)
            {
                TempData["ErrorMessage"] = "Çalışan bilgisi bulunamadı!";
                return RedirectToAction("Index", "Login");
            }

            // İstatistikler için hesaplamalar
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

            var emp = _context.Employees
                .Include(e => e.Department)
                    .ThenInclude(d => d.Manager)  // Departman yöneticisini dahil et
                .Include(e => e.Job)
                .Include(e => e.Manager)
                .Include(e => e.EmploymentContracts)
                .FirstOrDefault(e => e.EmployeeId == id);

            if (emp == null)
                return NotFound();

            return View(emp);
        }

        // GET: Employee/Create
        [HttpGet]
        public IActionResult Create()
        {
            // Dropdown listeleri için ViewBag'e veri yükleme
            ViewBag.Departments = _context.Departments
                .Select(d => new { d.DepartmentId, d.DepartmentName })
                .ToList();

            ViewBag.Jobs = _context.Jobs
                .Select(j => new { j.JobId, j.JobTitle })
                .ToList();

            ViewBag.Managers = _context.Users
                .Where(u => u.UserRoles.Any(ur =>
                    ur.Role.RoleName == "HR" || ur.Role.RoleName == "Admin"))
                .Select(u => new SelectListItem
                {
                    Value = u.UserId.ToString(),
                    Text = u.Username   // İstersen ad-soyad vs. property ekleyip kullan
                })
                .ToList();


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
            // Rol id'leri
            int hrRoleId = _context.Roles
                .Where(r => r.RoleName == "HR")
                .Select(r => r.RoleId)
                .FirstOrDefault();

            int employeeRoleId = _context.Roles
                .Where(r => r.RoleName == "Employee")
                .Select(r => r.RoleId)
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

                // Kullanıcı adı kontrolü
                if (_context.Users.Any(u => u.Username == username))
                {
                    ModelState.AddModelError("Username", "Bu kullanıcı adı zaten kullanılıyor.");
                    ReloadDropdowns();
                    return View(employee);
                }

                // User oluştur
                var user = new User
                {
                    Username = username,
                    UserPassword = userPassword,
                    Email = employee.Email,
                    IsActive = employee.IsActive
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                // Employee'ye User ID'yi ata
                employee.UserId = user.UserId;

                // Departman kontrolü ve rol ataması
                var department = _context.Departments
                    .FirstOrDefault(d => d.DepartmentId == employee.DepartmentId);

                if (department != null &&
                    !string.IsNullOrEmpty(department.DepartmentName) &&
                    department.DepartmentName.Equals("HR", StringComparison.OrdinalIgnoreCase))
                {
                    _context.UserRoles.Add(new UserRole
                    {
                        UserId = user.UserId,
                        RoleId = hrRoleId,
                        AssignedDate = DateTime.Now
                    });
                }

                // Her çalışana Employee rolü
                _context.UserRoles.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = employeeRoleId,
                    AssignedDate = DateTime.Now
                });

                // Employee kaydet
                _context.Employees.Add(employee);
                _context.SaveChanges();

                // Sözleşme oluşturma
                if (createContract && contractStartDate.HasValue && contractSalary.HasValue)
                {
                    var contract = new EmploymentContract
                    {
                        EmployeeId = employee.EmployeeId,
                        StartDate = contractStartDate.Value,
                        EndDate = contractEndDate,
                        Salary = contractSalary.Value,
                        ContractType = contractType ?? "Belirsiz Süreli",
                        IsActive = true
                    };

                    _context.EmploymentContracts.Add(contract);
                    _context.SaveChanges();
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
            ViewBag.Departments = _context.Departments
                .Select(d => new { d.DepartmentId, d.DepartmentName })
                .ToList();

            ViewBag.Jobs = _context.Jobs
                .Select(j => new { j.JobId, j.JobTitle })
                .ToList();

            ViewBag.Managers = _context.Users
                .Where(u => u.UserRoles.Any(ur =>
                    ur.Role.RoleName == "HR" || ur.Role.RoleName == "Admin"))
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

            if (id == employeeId || userId==id)
            {
                TempData["ErrorMessage"] = "Kendi profilinizi silemezsiniz!";
                return RedirectToAction("Index", "HR");
            }

            var emp = _context.Employees.FirstOrDefault(e => e.EmployeeId == id);

            // 1) Manager ise, astların ManagerID'sini NULL yap
            var subordinates = _context.Employees.Where(e => e.ManagerId == id).ToList();
            foreach (var s in subordinates)
                s.ManagerId = null;

            // 2) Reviewer ise, PerformanceReview.ReviewerID NULL yapılmalı
            var reviewsReviewed = _context.PerformanceReviews.Where(r => r.ReviewerId == id).ToList();
            foreach (var r in reviewsReviewed)
                r.ReviewerId = null;


            //user kaydınıda sil
            if (emp.UserId != null)
            {
                var user = _context.Users.FirstOrDefault(u => u.UserId == emp.UserId);
                _context.Users.Remove(user);
            }

            _context.Employees.Remove(emp);
            _context.SaveChanges();
            return RedirectToAction("Index","HR");
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var employeeId = HttpContext.Session.GetInt32("EmployeeId");
            var userId = HttpContext.Session.GetInt32("UserId");

            if (id == employeeId || userId == id)
            {
                TempData["ErrorMessage"] = "Kendi profilinizi Güncelleyemezsiniz!";
                return RedirectToAction("Index", "HR");
            }

            // 1) Employee'i bul (Sözleşme ile birlikte)
            var emp = _context.Employees
                .Include(e => e.EmploymentContracts)
                .Include(e => e.Department)
                .FirstOrDefault(e => e.EmployeeId == id);

            if (emp == null)
                return NotFound();
            // Bu employee herhangi bir departmanın yöneticisi mi?
            bool isDepartmentManager = _context.Departments
                .Any(d => d.ManagerId == emp.EmployeeId);

            ViewBag.IsDepartmentManager = isDepartmentManager;

            ViewBag.Departments = _context.Departments
                .Select(d => new SelectListItem
                {
                    Value = d.DepartmentId.ToString(),
                    Text = d.DepartmentName,
                    Selected = d.DepartmentId == emp.DepartmentId
                })
                .ToList();

            ViewBag.Jobs = _context.Jobs
                .Select(j => new SelectListItem
                {
                    Value = j.JobId.ToString(),
                    Text = j.JobTitle,
                    Selected = j.JobId == emp.JobId
                })
                .ToList();

            // Edit için de sadece HR & Admin rollerine sahip kullanıcılar
            ViewBag.Managers = _context.Users
                .Where(u => u.UserRoles.Any(ur =>
                    ur.Role.RoleName == "HR" || ur.Role.RoleName == "Admin"))
                .Select(u => new SelectListItem
                {
                    Value = u.UserId.ToString(),
                    Text = u.Username,
                    Selected = (emp.ManagerId != null && u.UserId == emp.ManagerId)
                })
                .ToList();

            // 3) Aktif sözleşmeyi ViewBag'e ekle
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
            // DEBUG: ModelState hatalarını logla
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .Select(x => new {
                        Field = x.Key,
                        Errors = x.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    })
                    .ToList();

                ViewBag.ValidationErrors = errors;
                ReloadDropdownsForEdit(model);

                // Aktif sözleşmeyi yeniden yükle
                var emp = _context.Employees.Include(e => e.EmploymentContracts).FirstOrDefault(e => e.EmployeeId == model.EmployeeId);
                ViewBag.ActiveContract = emp?.EmploymentContracts?.FirstOrDefault(c => c.IsActive);

                return View(model);
            }

            var employee = _context.Employees
                .Include(e => e.EmploymentContracts)
                .FirstOrDefault(e => e.EmployeeId == model.EmployeeId);

            if (employee == null)
                return NotFound();

            bool isDepartmentManager = _context.Departments
                .Any(d => d.ManagerId == employee.EmployeeId);

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
            ViewBag.Departments = _context.Departments
                .Select(d => new SelectListItem
                {
                    Value = d.DepartmentId.ToString(),
                    Text = d.DepartmentName,
                    Selected = d.DepartmentId == model.DepartmentId
                })
                .ToList();

            ViewBag.Jobs = _context.Jobs
                .Select(j => new SelectListItem
                {
                    Value = j.JobId.ToString(),
                    Text = j.JobTitle,
                    Selected = j.JobId == model.JobId
                })
                .ToList();

            ViewBag.Managers = _context.Users
                .Where(u => u.UserRoles.Any(ur =>
                    ur.Role.RoleName == "HR" || ur.Role.RoleName == "Admin"))
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

            var emp = _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Job)
                .Include(e => e.Attendances)
                .FirstOrDefault(e => e.EmployeeId == id);

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
                
                // Bugün zaten giriş yapmış mı kontrol et
                var existingAttendance = _context.Attendances
                    .FirstOrDefault(a => a.EmployeeId == employeeId && a.Date == today);

                if (existingAttendance != null)
                {
                    TempData["ErrorMessage"] = "Bugün için zaten giriş kaydı mevcut!";
                    return RedirectToAction("Attendance", new { id = employeeId });
                }

                // Yeni giriş kaydı oluştur
                var attendance = new Attendance
                {
                    EmployeeId = employeeId,
                    Date = today,
                    CheckInTime = DateTime.Now,
                    CheckOutTime = null
                };

                _context.Attendances.Add(attendance);
                _context.SaveChanges();

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
                var attendance = _context.Attendances.Find(attendanceId);

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

                attendance.CheckOutTime = DateTime.Now;
                _context.SaveChanges();

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
            var emp = _context.Employees
                .Include(e => e.LeaveRequests)
                .ThenInclude(l => l.LeaveType)
                .FirstOrDefault(e => e.EmployeeId == id);

            if (emp == null) return NotFound();

            return View(emp);
        }

        public IActionResult Performance(int id)
        {
            var emp = _context.Employees
                .Include(e => e.PerformanceReviewEmployees)  // EmployeeId ile ilişkililer
                .FirstOrDefault(e => e.EmployeeId == id);

            if (emp == null) return NotFound();

            return View(emp);
        }

        public IActionResult Documents(int id)
        {
            var emp = _context.Employees
                .Include(e => e.Documents)
                .FirstOrDefault(e => e.EmployeeId == id);

            if (emp == null) return NotFound();

            return View(emp);
        }

    }



}
