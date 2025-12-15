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

        public IActionResult Index() // HR Dashboard
        {
            // Çalışan listesi (Tümü) – SQL + Include
            const string sqlEmp = @"SELECT * FROM Employees";

            var emp = _context.Employees
                .FromSqlRaw(sqlEmp)
                .Include(e => e.Department)
                .Include(e => e.Job)
                .Include(e => e.Manager)
                .AsEnumerable()
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .ToList();

            // Dashboard statistics 
            ViewBag.TotalEmployees = emp.Count;

            // TotalDepartments – SQL
            const string sqlTotalDeps = "SELECT COUNT(*) AS Value FROM Departments";
            ViewBag.TotalDepartments = _context.Database
                .SqlQueryRaw<int>(sqlTotalDeps)
                .AsEnumerable()
                .FirstOrDefault();

            ViewBag.ActiveEmployees = emp.Count(e => e.IsActive);

            ViewBag.RecentHires = emp
                .Where(e => e.HireDate.HasValue &&
                            e.HireDate.Value >= DateOnly.FromDateTime(DateTime.Now.AddDays(-30)))
                .Count();

            // Pending leave requests – SQL
            const string sqlPendingLeaves = @"
                SELECT COUNT(*) AS Value
                FROM LeaveRequests
                WHERE Status = 'Pending'";
            ViewBag.PendingLeaves = _context.Database
                .SqlQueryRaw<int>(sqlPendingLeaves)
                .AsEnumerable()
                .FirstOrDefault();

            // Employees on leave today – SQL
            var today = DateOnly.FromDateTime(DateTime.Now);
            const string sqlTodayOnLeave = @"
                SELECT COUNT(*) AS Value
                FROM LeaveRequests
                WHERE StartDate <= {0}
                  AND EndDate   >= {0}
                  AND Status = 'Approved'";

            ViewBag.TodayOnLeave = _context.Database
                .SqlQueryRaw<int>(sqlTodayOnLeave, today)
                .AsEnumerable()
                .FirstOrDefault();

            return View(emp);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id) // id: silinecek UserId (HR/Admin)
        {
            // User + Employee – SQL
            const string sqlUser = @"
                SELECT * FROM Users
                WHERE UserID = {0}";
            var user = _context.Users
                .FromSqlRaw(sqlUser, id)
                .Include(u => u.Employee) // varsa bağlı employee
                .AsEnumerable()
                .FirstOrDefault();

            if (user == null)
                return NotFound();

            // 1) Bu kullanıcının bir Employee kaydı varsa ve o employee manager ise,
            //    yönettiği tüm çalışanların ManagerId'sini NULL yap
            if (user.Employee != null)
            {
                var managerEmployeeId = user.Employee.EmployeeId;

                // Astlar – SQL
                const string sqlSubs = @"
                    SELECT * FROM Employees
                    WHERE ManagerID = {0}";
                var managedEmployees = _context.Employees
                    .FromSqlRaw(sqlSubs, managerEmployeeId)
                    .ToList();

                foreach (var emp in managedEmployees)
                {
                    emp.ManagerId = null;
                }

                // Yönetici olduğu performans kayıtları – SQL
                const string sqlReviews = @"
                    SELECT * FROM PerformanceReviews
                    WHERE ReviewerID = {0}";
                var reviewsReviewed = _context.PerformanceReviews
                    .FromSqlRaw(sqlReviews, managerEmployeeId)
                    .ToList();

                foreach (var review in reviewsReviewed)
                {
                    review.ReviewerId = null;
                }

                _context.SaveChanges();

                // Employee kaydını sil
                const string sqlDeleteEmp = @"DELETE FROM Employees WHERE EmployeeID = {0}";
                _context.Database.ExecuteSqlRaw(sqlDeleteEmp, user.Employee.EmployeeId);
            }

            // 2) Kullanıcıyı sil – önce UserRoles, sonra User
            const string sqlDeleteUserRoles = @"DELETE FROM UserRoles WHERE UserID = {0}";
            _context.Database.ExecuteSqlRaw(sqlDeleteUserRoles, id);

            const string sqlDeleteUser = @"DELETE FROM Users WHERE UserID = {0}";
            _context.Database.ExecuteSqlRaw(sqlDeleteUser, id);

            return RedirectToAction("Index");
        }
    }
}