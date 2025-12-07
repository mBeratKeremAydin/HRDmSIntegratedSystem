using HRDms.Data.Context;
using HRDms.Data.Models;
using Microsoft.AspNetCore.Mvc;
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

        // Admin Dashboard
        public IActionResult Index()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userRole != "Admin" || userId == null)
            {
                TempData["ErrorMessage"] = "Bu sayfayı görüntüleme yetkiniz yok!";
                return RedirectToAction("Index", "Login");
            }

            // İstatistikler
            ViewBag.TotalEmployees = _context.Employees.Count();
            ViewBag.ActiveEmployees = _context.Employees.Count(e => e.IsActive);
            ViewBag.TotalDepartments = _context.Departments.Count();
            ViewBag.TotalJobs = _context.Jobs.Count();
            ViewBag.TotalContracts = _context.EmploymentContracts.Count();
            ViewBag.ActiveContracts = _context.EmploymentContracts.Count(c => c.IsActive);
            ViewBag.PendingLeaves = _context.LeaveRequests.Count(l => l.Status == "Pending");
            ViewBag.TotalPerformanceReviews = _context.PerformanceReviews.Count();
            ViewBag.TotalUsers = _context.Users.Count();
            ViewBag.ActiveUsers = _context.Users.Count(u => u.IsActive);

            // Son aktiviteler
            ViewBag.RecentEmployees = _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Job)
                .OrderByDescending(e => e.EmployeeId)
                .Take(5)
                .ToList();

            ViewBag.RecentLeaves = _context.LeaveRequests
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .Where(l => l.Status == "Pending")
                .OrderByDescending(l => l.RequestId)
                .Take(5)
                .ToList();

            ViewBag.RecentContracts = _context.EmploymentContracts
                .Include(c => c.Employee)
                .OrderByDescending(c => c.ContractId)
                .Take(5)
                .ToList();

            return View();
        }

        // Çalışan Yönetimi
        public IActionResult Employees()
        {
            var employees = _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Job)
                .Include(e => e.Manager)
                .OrderBy(e => e.FirstName)
                .ToList();

            return View(employees);
        }

        // Departman Yönetimi
        public IActionResult Departments()
        {
            return RedirectToAction("Index", "Department");
        }

        // İş Pozisyonları Yönetimi
        public IActionResult Jobs()
        {
            var jobs = _context.Jobs
                .Include(j => j.Employees)
                .OrderBy(j => j.JobTitle)
                .ToList();

            return View(jobs);
        }

        // Sözleşme Yönetimi
        public IActionResult Contracts()
        {
            var contracts = _context.EmploymentContracts
                .Include(c => c.Employee)
                    .ThenInclude(e => e.Department)
                .Include(c => c.Employee)
                    .ThenInclude(e => e.Job)
                .OrderByDescending(c => c.StartDate)
                .ToList();

            return View(contracts);
        }

        // İzin Talepleri Yönetimi
        public IActionResult LeaveRequests()
        {
            return RedirectToAction("Index", "LeaveRequests");
        }

        // Performans Değerlendirmeleri
        public IActionResult PerformanceReviews()
        {
            var reviews = _context.PerformanceReviews
                .Include(p => p.Employee)
                    .ThenInclude(e => e.Department)
                .Include(p => p.Reviewer)
                .OrderByDescending(p => p.ReviewDate)
                .ToList();

            return View(reviews);
        }

        // Kullanıcı Yönetimi
        public IActionResult Users()
        {
            var users = _context.Users
                .Include(u => u.Employee)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .OrderBy(u => u.Username)
                .ToList();

            return View(users);
        }

        // Sistem Ayarları
        public IActionResult Settings()
        {
            ViewBag.LeaveTypes = _context.LeaveTypes.ToList();
            ViewBag.Locations = _context.Locations.ToList();
            ViewBag.Roles = _context.Roles.ToList();

            return View();
        }

        // İstatistikler ve Raporlar
        public IActionResult Reports()
        {
            // Departman bazlı çalışan sayıları
            ViewBag.DepartmentStats = _context.Departments
                .Include(d => d.Employees)
                .Select(d => new
                {
                    DepartmentName = d.DepartmentName,
                    EmployeeCount = d.Employees.Count,
                    ActiveCount = d.Employees.Count(e => e.IsActive)
                })
                .ToList();

            // Aylık izin talepleri
            ViewBag.MonthlyLeaves = _context.LeaveRequests
                .Where(l => l.StartDate.Year == DateTime.Now.Year)
                .GroupBy(l => l.StartDate.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Count = g.Count()
                })
                .ToList();

            // Sözleşme türleri dağılımı
            ViewBag.ContractTypes = _context.EmploymentContracts
                .Where(c => c.IsActive)
                .GroupBy(c => c.ContractType)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count()
                })
                .ToList();

            return View();
        }
    }
}
