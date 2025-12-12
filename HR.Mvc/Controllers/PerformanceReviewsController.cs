using HRDms.Data.Context;
using HR.Mvc.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HR.Mvc.Controllers
{
    public class PerformanceReviewsController : Controller
    {
        private readonly AppDbContext _context;

        public PerformanceReviewsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index(int employeeId)
        {
            if (employeeId <= 0)
                return BadRequest("Index GET: employeeId boş veya 0.");

            var sqlInfo = @"
                SELECT e.EmployeeID, e.FirstName, e.LastName, j.JobTitle, d.DepartmentName
                FROM Employees e
                LEFT JOIN Jobs j ON e.JobID = j.JobID
                LEFT JOIN Departments d ON e.DepartmentID = d.DepartmentID
                WHERE e.EmployeeID = {0}";

            var basicInfo = _context.Database.SqlQueryRaw<EmployeeBasicInfoDTO>(sqlInfo, employeeId)
                .AsEnumerable()
                .FirstOrDefault();

            if (basicInfo == null)
                return NotFound("Employee bulunamadı.");

            var model = new EmployeePerformanceViewModel
            {
                EmployeeId = basicInfo.EmployeeId,
                FirstName = basicInfo.FirstName,
                LastName = basicInfo.LastName,
                JobTitle = basicInfo.JobTitle,
                DepartmentName = basicInfo.DepartmentName
            };

            var sqlReviews = @"
                SELECT pr.ReviewId, pr.EmployeeId, pr.ReviewDate, pr.Score, pr.Notes, 
                       r.FirstName + ' ' + r.LastName AS ReviewerName
                FROM PerformanceReviews pr
                LEFT JOIN Employees r ON pr.ReviewerID = r.EmployeeID
                WHERE pr.EmployeeID = {0}
                ORDER BY pr.ReviewDate DESC";

            model.Reviews = _context.Database.SqlQueryRaw<PerformanceReviewViewModel>(sqlReviews, employeeId).ToList();

            return View(model);
        }

        [HttpGet]
        public IActionResult Create(int employeeId)
        {
            if (employeeId <= 0)
                return BadRequest("Create GET: employeeId boş veya 0.");

            var sqlEmployee = "SELECT FirstName + ' ' + LastName AS Value FROM Employees WHERE EmployeeID = {0}";
            var employeeName = _context.Database.SqlQueryRaw<string>(sqlEmployee, employeeId).AsEnumerable().FirstOrDefault();

            if (employeeName == null)
                return NotFound("Employee bulunamadı.");

            var model = new PerformanceReviewCreateViewModel
            {
                EmployeeId = employeeId
            };

            ViewBag.EmployeeName = employeeName;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(PerformanceReviewCreateViewModel model)
        {
            if (model.EmployeeId <= 0)
                return BadRequest("POST: EmployeeId 0 geldi. Hidden alanı kontrol et.");

            var sessionEmployeeId = HttpContext.Session.GetInt32("EmployeeId");
            if (!sessionEmployeeId.HasValue)
                return BadRequest("POST: Session.EmployeeId yok (login olunmamış).");

            if (!ModelState.IsValid)
            {
                var sqlEmployee = "SELECT FirstName + ' ' + LastName AS Value FROM Employees WHERE EmployeeID = {0}";
                ViewBag.EmployeeName = _context.Database.SqlQueryRaw<string>(sqlEmployee, model.EmployeeId).AsEnumerable().FirstOrDefault();
                return View(model);
            }

            var sqlInsert = @"
                INSERT INTO PerformanceReviews (EmployeeID, ReviewerID, ReviewDate, Score, Notes)
                VALUES ({0}, {1}, {2}, {3}, {4})";

            _context.Database.ExecuteSqlRaw(sqlInsert, 
                model.EmployeeId, 
                sessionEmployeeId.Value, 
                model.ReviewDate, 
                model.Score, 
                model.Notes);

            return RedirectToAction("Index", new { employeeId = model.EmployeeId });
        }
    }
}
