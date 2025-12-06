using HRDms.Data.Data;
using Microsoft.AspNetCore.Mvc;

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
            var emp = _context.Employees.Include(e=>e.Department).ToList();
            return View(emp);
        }
    }
}
