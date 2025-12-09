using HRDms.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HR.Mvc.Controllers
{
    public class HRController : Controller
    {
        private readonly AppDbContext _context;

        public HRController(AppDbContext context)
        {
            _context = context;
        }
        public IActionResult Index() //HR Dashboard
        {
            var emp = _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Job)
                .Include(e => e.Manager)
                .Where(e => e.IsActive)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .ToList();

            // Dashboard statistics
            ViewBag.TotalEmployees = emp.Count;
            ViewBag.TotalDepartments = _context.Departments.Count();
            ViewBag.ActiveEmployees = emp.Count(e => e.IsActive);
            ViewBag.RecentHires = emp.Where(e => e.HireDate.HasValue &&
                e.HireDate.Value >= DateOnly.FromDateTime(DateTime.Now.AddDays(-30))).Count();

            // NEW: HR-specific statistics
            // Pending leave requests (assuming you have a Leaves table with Status field)
            ViewBag.PendingLeaves = _context.LeaveRequests?
                .Count(l => l.Status == "Pending") ?? 0;

            // Employees on leave today
            var today = DateOnly.FromDateTime(DateTime.Now);
            ViewBag.TodayOnLeave = _context.LeaveRequests?
                .Count(l => l.StartDate <= today && l.EndDate >= today && l.Status == "Approved") ?? 0;

            return View(emp);
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id) // id: silinecek UserId (HR/Admin)
        {
            var user = _context.Users
                .Include(u => u.Employee) // varsa bağlı employee
                .FirstOrDefault(u => u.UserId == id);

            if (user == null)
                return NotFound();

            // 1) Bu kullanıcının bir Employee kaydı varsa ve o employee manager ise,
            //    yönettiği tüm çalışanların ManagerId'sini NULL yap
            if (user.Employee != null)
            {
                var managerEmployeeId = user.Employee.EmployeeId;

                var managedEmployees = _context.Employees
                    .Where(e => e.ManagerId == managerEmployeeId)
                    .ToList();

                foreach (var emp in managedEmployees)
                {
                    emp.ManagerId = null;
                }

                var reviewsReviewed = _context.PerformanceReviews
                    .Where(r => r.ReviewerId == managerEmployeeId)
                    .ToList();

                foreach (var review in reviewsReviewed)
                {
                    review.ReviewerId = null;
                }

            }

            // 2) Kullanıcıyı sil
            _context.Users.Remove(user);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

    }
}
