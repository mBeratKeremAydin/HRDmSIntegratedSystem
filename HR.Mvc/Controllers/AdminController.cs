using HRDms.Data.Context;
using HRDms.Data.Models;
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

        // Ana Dashboard
        public IActionResult Index()
        {
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Bu sayfaya erişim yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }
                    
            // İstatistikler
            ViewBag.TotalEmployees = _context.Employees.Count();
            ViewBag.TotalHREmployees = _context.Employees
                .Include(e => e.Department)
                .Count(e => e.Department.DepartmentName == "HR");
            ViewBag.TotalJobs = _context.Jobs.Count();
            ViewBag.TotalLeaveTypes = _context.LeaveTypes.Count();
            ViewBag.TotalLocations = _context.Locations.Count();
            ViewBag.TotalRoles = _context.Roles.Count();
            ViewBag.TotalDepartments = _context.Departments.Count();

            return View();
        }

        #region HR Employee Management

        // HR Çalışanları Listele
        public IActionResult HREmployees()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            var hrDepartment = _context.Departments
                .FirstOrDefault(d => d.DepartmentName == "HR");

            if (hrDepartment == null)
            {
                TempData["ErrorMessage"] = "HR departmanı bulunamadı!";
                return RedirectToAction("Index");
            }

            var hrEmployees = _context.Employees
                .Include(e => e.Job)
                .Include(e => e.User)
                .Include(e => e.Department)
                .Where(e => e.DepartmentId == hrDepartment.DepartmentId)
                .ToList();

            return View(hrEmployees);
        }

        // HR Çalışan Ekleme - GET
        [HttpGet]
        public IActionResult CreateHREmployee()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            // HR Departmanını bul
            var hrDepartment = _context.Departments
                .FirstOrDefault(d => d.DepartmentName == "HR");

            if (hrDepartment == null)
            {
                TempData["ErrorMessage"] = "HR departmanı bulunamadı! Önce HR departmanı oluşturun.";
                return RedirectToAction("Index");
            }

            ViewBag.HRDepartmentId = hrDepartment.DepartmentId;
            ViewBag.HRDepartmentName = hrDepartment.DepartmentName;

            ViewBag.Jobs = new SelectList(_context.Jobs, "JobId", "JobTitle");

            return View();
        }

        // HR Çalışan Ekleme - POST
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

            // HR Departmanını bul
            var hrDepartment = _context.Departments
                .FirstOrDefault(d => d.DepartmentName == "HR");

            if (hrDepartment == null)
            {
                TempData["ErrorMessage"] = "HR departmanı bulunamadı!";
                return RedirectToAction("Index");
            }

            // DepartmentId'yi zorla HR yap
            employee.DepartmentId = hrDepartment.DepartmentId;

            // Navigation property'leri temizle
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
                ViewBag.HRDepartmentId = hrDepartment.DepartmentId;
                ViewBag.HRDepartmentName = hrDepartment.DepartmentName;
                ViewBag.Jobs = new SelectList(_context.Jobs, "JobId", "JobTitle", employee.JobId);
                return View(employee);
            }

            if (_context.Users.Any(u => u.Username == username))
            {
                ModelState.AddModelError("", "Bu kullanıcı adı zaten kullanılıyor!");
                ViewBag.HRDepartmentId = hrDepartment.DepartmentId;
                ViewBag.HRDepartmentName = hrDepartment.DepartmentName;
                ViewBag.Jobs = new SelectList(_context.Jobs, "JobId", "JobTitle", employee.JobId);
                return View(employee);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.HRDepartmentId = hrDepartment.DepartmentId;
                ViewBag.HRDepartmentName = hrDepartment.DepartmentName;
                ViewBag.Jobs = new SelectList(_context.Jobs, "JobId", "JobTitle", employee.JobId);
                return View(employee);
            }

            try
            {
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

                // Employee'ye User ID ata
                employee.UserId = user.UserId;

                // Employee kaydet
                _context.Employees.Add(employee);
                _context.SaveChanges();

                // HR Rolü ata
                var hrRoleId = _context.Roles
                    .Where(r => r.RoleName == "HR")
                    .Select(r => r.RoleId)
                    .FirstOrDefault();

                var employeeRoleId = _context.Roles
                    .Where(r => r.RoleName == "Employee")
                    .Select(r => r.RoleId)
                    .FirstOrDefault();

                if (hrRoleId != 0)
                {
                    _context.UserRoles.Add(new UserRole
                    {
                        UserId = user.UserId,
                        RoleId = hrRoleId,
                        AssignedDate = DateTime.Now
                    });
                }

                if (employeeRoleId != 0)
                {
                    _context.UserRoles.Add(new UserRole
                    {
                        UserId = user.UserId,
                        RoleId = employeeRoleId,
                        AssignedDate = DateTime.Now
                    });
                }

                _context.SaveChanges();

                TempData["SuccessMessage"] = "HR çalışanı başarıyla eklendi!";
                return RedirectToAction("HREmployees");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                ViewBag.HRDepartmentId = hrDepartment.DepartmentId;
                ViewBag.HRDepartmentName = hrDepartment.DepartmentName;
                ViewBag.Jobs = new SelectList(_context.Jobs, "JobId", "JobTitle", employee.JobId);
                return View(employee);
            }
        }

        // HR Çalışan Silme
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

            var employee = _context.Employees
                .Include(e => e.User)
                .FirstOrDefault(e => e.EmployeeId == id);

            if (employee == null)
            {
                TempData["ErrorMessage"] = "Çalışan bulunamadı!";
                return RedirectToAction("HREmployees");
            }

            try
            {
                // İlişkili kayıtları temizle
                var userRoles = _context.UserRoles.Where(ur => ur.UserId == employee.UserId);
                _context.UserRoles.RemoveRange(userRoles);

                if (employee.User != null)
                {
                    _context.Users.Remove(employee.User);
                }

                _context.Employees.Remove(employee);
                _context.SaveChanges();

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

        // Jobs Listele
        public IActionResult Jobs()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            var jobs = _context.Jobs.ToList();
            return View(jobs);
        }

        // Job Ekleme - GET
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

        // Job Ekleme - POST
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

            // Navigation property'leri temizle
            ModelState.Remove("Employees");

            if (!ModelState.IsValid)
            {
                return View(job);
            }

            try
            {
                _context.Jobs.Add(job);
                _context.SaveChanges();

                TempData["SuccessMessage"] = "İş pozisyonu başarıyla eklendi!";
                return RedirectToAction("Jobs");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(job);
            }
        }

        // Job Düzenleme - GET
        [HttpGet]
        public IActionResult EditJob(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            var job = _context.Jobs.Find(id);
            if (job == null)
            {
                TempData["ErrorMessage"] = "İş pozisyonu bulunamadı!";
                return RedirectToAction("Jobs");
            }

            return View(job);
        }

        // Job Düzenleme - POST
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

            // Navigation property'leri temizle
            ModelState.Remove("Employees");

            if (!ModelState.IsValid)
            {
                return View(job);
            }

            try
            {
                var existingJob = _context.Jobs.Find(job.JobId);
                if (existingJob == null)
                {
                    TempData["ErrorMessage"] = "İş pozisyonu bulunamadı!";
                    return RedirectToAction("Jobs");
                }

                existingJob.JobTitle = job.JobTitle;
                existingJob.MinSalary = job.MinSalary;
                existingJob.MaxSalary = job.MaxSalary;

                _context.SaveChanges();

                TempData["SuccessMessage"] = "İş pozisyonu başarıyla güncellendi!";
                return RedirectToAction("Jobs");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(job);
            }
        }

        // Job Silme
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

            var job = _context.Jobs.Find(id);
            if (job == null)
            {
                TempData["ErrorMessage"] = "İş pozisyonu bulunamadı!";
                return RedirectToAction("Jobs");
            }

            // İlişkili employee var mı kontrol et
            var hasEmployees = _context.Employees.Any(e => e.JobId == id);
            if (hasEmployees)
            {
                TempData["ErrorMessage"] = "Bu iş pozisyonuna bağlı çalışanlar var! Önce çalışanların pozisyonunu değiştirin.";
                return RedirectToAction("Jobs");
            }

            try
            {
                _context.Jobs.Remove(job);
                _context.SaveChanges();

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

        // Leave Types Listele
        public IActionResult LeaveTypes()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            var leaveTypes = _context.LeaveTypes.ToList();
            return View(leaveTypes);
        }

        // Leave Type Ekleme - GET
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

        // Leave Type Ekleme - POST
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

            // Navigation property'leri temizle
            ModelState.Remove("LeaveRequests");

            if (!ModelState.IsValid)
            {
                return View(leaveType);
            }

            try
            {
                _context.LeaveTypes.Add(leaveType);
                _context.SaveChanges();

                TempData["SuccessMessage"] = "İzin türü başarıyla eklendi!";
                return RedirectToAction("LeaveTypes");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(leaveType);
            }
        }

        // Leave Type Düzenleme - GET
        [HttpGet]
        public IActionResult EditLeaveType(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            var leaveType = _context.LeaveTypes.Find(id);
            if (leaveType == null)
            {
                TempData["ErrorMessage"] = "İzin türü bulunamadı!";
                return RedirectToAction("LeaveTypes");
            }

            return View(leaveType);
        }

        // Leave Type Düzenleme - POST
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

            // Navigation property'leri temizle
            ModelState.Remove("LeaveRequests");

            if (!ModelState.IsValid)
            {
                return View(leaveType);
            }

            try
            {
                var existingLeaveType = _context.LeaveTypes.Find(leaveType.LeaveTypeId);
                if (existingLeaveType == null)
                {
                    TempData["ErrorMessage"] = "İzin türü bulunamadı!";
                    return RedirectToAction("LeaveTypes");
                }

                existingLeaveType.TypeName = leaveType.TypeName;
                existingLeaveType.DaysAllowed = leaveType.DaysAllowed;

                _context.SaveChanges();

                TempData["SuccessMessage"] = "İzin türü başarıyla güncellendi!";
                return RedirectToAction("LeaveTypes");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(leaveType);
            }
        }

        // Leave Type Silme
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

            var leaveType = _context.LeaveTypes.Find(id);
            if (leaveType == null)
            {
                TempData["ErrorMessage"] = "İzin türü bulunamadı!";
                return RedirectToAction("LeaveTypes");
            }

            // İlişkili leave request var mı kontrol et
            var hasRequests = _context.LeaveRequests.Any(lr => lr.LeaveTypeId == id);
            if (hasRequests)
            {
                TempData["ErrorMessage"] = "Bu izin türüne bağlı izin talepleri var! Silinemez.";
                return RedirectToAction("LeaveTypes");
            }

            try
            {
                _context.LeaveTypes.Remove(leaveType);
                _context.SaveChanges();

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

        // Locations Listele
        public IActionResult Locations()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            var locations = _context.Locations.ToList();
            return View(locations);
        }

        // Location Ekleme - GET
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

        // Location Ekleme - POST
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

            // Navigation property'leri temizle
            ModelState.Remove("Departments");

            if (!ModelState.IsValid)
            {
                return View(location);
            }

            try
            {
                _context.Locations.Add(location);
                _context.SaveChanges();

                TempData["SuccessMessage"] = "Lokasyon başarıyla eklendi!";
                return RedirectToAction("Locations");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(location);
            }
        }

        // Location Düzenleme - GET
        [HttpGet]
        public IActionResult EditLocation(int id)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            var location = _context.Locations.Find(id);
            if (location == null)
            {
                TempData["ErrorMessage"] = "Lokasyon bulunamadı!";
                return RedirectToAction("Locations");
            }

            return View(location);
        }

        // Location Düzenleme - POST
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

            // Navigation property'leri temizle
            ModelState.Remove("Departments");

            if (!ModelState.IsValid)
            {
                return View(location);
            }

            try
            {
                var existingLocation = _context.Locations.Find(location.LocationId);
                if (existingLocation == null)
                {
                    TempData["ErrorMessage"] = "Lokasyon bulunamadı!";
                    return RedirectToAction("Locations");
                }

                existingLocation.LocationName = location.LocationName;
                existingLocation.Address = location.Address;

                _context.SaveChanges();

                TempData["SuccessMessage"] = "Lokasyon başarıyla güncellendi!";
                return RedirectToAction("Locations");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Bir hata oluştu: " + ex.Message);
                return View(location);
            }
        }

        // Location Silme
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

            var location = _context.Locations.Find(id);
            if (location == null)
            {
                TempData["ErrorMessage"] = "Lokasyon bulunamadı!";
                return RedirectToAction("Locations");
            }

            // İlişkili department var mı kontrol et
            var hasDepartments = _context.Departments.Any(d => d.LocationId == id);
            if (hasDepartments)
            {
                TempData["ErrorMessage"] = "Bu lokasyona bağlı departmanlar var! Önce departmanların lokasyonunu değiştirin.";
                return RedirectToAction("Locations");
            }

            try
            {
                _context.Locations.Remove(location);
                _context.SaveChanges();

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

        // Roles Listele
        public IActionResult Roles()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin")
            {
                TempData["ErrorMessage"] = "Yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            var roles = _context.Roles.ToList();
            return View(roles);
        }

        // Role Ekleme - GET
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

        // Role Ekleme - POST
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

            // Navigation property'leri temizle
            ModelState.Remove("UserRoles");

            if (!ModelState.IsValid)
            {
                return View(role);
            }

            // Aynı isimde rol var mı kontrol et
            if (_context.Roles.Any(r => r.RoleName == role.RoleName))
            {
                ModelState.AddModelError("", "Bu rol adı zaten mevcut!");
                return View(role);
            }

            try
            {
                _context.Roles.Add(role);
                _context.SaveChanges();

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
