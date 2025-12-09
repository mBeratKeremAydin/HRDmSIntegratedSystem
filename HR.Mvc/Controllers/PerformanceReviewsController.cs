using HRDms.Data.Context;
using HRDms.Data.Models;
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

            var employee = _context.Employees
                .Include(e => e.Job)
                .Include(e => e.PerformanceReviewEmployees)
                    .ThenInclude(r => r.Reviewer)
                .FirstOrDefault(e => e.EmployeeId == employeeId);

            if (employee == null)
                return NotFound("Employee bulunamadı.");

            return View(employee);
        }

        [HttpGet]
        public IActionResult Create(int employeeId)
        {
            if (employeeId <= 0)
                return BadRequest("Create GET: employeeId boş veya 0.");

            var employee = _context.Employees.Find(employeeId);
            if (employee == null)
                return NotFound("Employee bulunamadı.");

            var model = new PerformanceReview
            {
                EmployeeId = employeeId,
                ReviewDate = DateOnly.FromDateTime(DateTime.Now)
            };

            ViewBag.EmployeeName = employee.FirstName + " " + employee.LastName;
            return View(model);
        }

        [HttpPost]
        //[ValidateAntiForgeryToken]
        public IActionResult Create([Bind("EmployeeId,ReviewDate,Score,Notes")] PerformanceReview model)
        {
            // navigation alanları yok; zaten bind etmiyoruz
            ModelState.Remove("Employee");
            ModelState.Remove("Reviewer");

            if (model.EmployeeId <= 0)
                return BadRequest("POST: EmployeeId 0 geldi. Hidden alanı kontrol et.");

            var sessionEmployeeId = HttpContext.Session.GetInt32("EmployeeId");
            if (!sessionEmployeeId.HasValue)
                return BadRequest("POST: Session.EmployeeId yok (login olunmamış).");

            // ReviewerId’yi SESSION’dan ata
            model.ReviewerId = sessionEmployeeId.Value;

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ",
                    ModelState.Where(x => x.Value.Errors.Count > 0)
                             .Select(x => $"{x.Key}: {string.Join(",", x.Value.Errors.Select(e => e.ErrorMessage))}"));

                return BadRequest("POST: ModelState geçersiz. " + errors);
            }

            _context.PerformanceReviews.Add(model);
            _context.SaveChanges();

            return RedirectToAction("Index", new { employeeId = model.EmployeeId });
        }
    }
}
