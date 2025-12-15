using HRDms.Data.Context;
using DMS.Mvc.Models;
using HRDms.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DMS.Mvc.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // Oturum Kontrolü
            int? employeeId = HttpContext.Session.GetInt32("EmployeeID");
            if (employeeId == null) return RedirectToAction("Login", "Account");

            var model = new DashboardViewModel();

            // 1. SORGU: Toplam Çalýþan Sayýsý (HR Verisi)
            

            model.TotalEmployeeCount = _context.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) as Value FROM Employees WHERE IsActive = 1")
                .AsEnumerable()
                .FirstOrDefault();

            // Giriþ yapan kiþinin departman ID'sini alýyoruz
            int? myDeptId = HttpContext.Session.GetInt32("DepartmentID");

            model.DepartmentEmployeeCount = _context.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) as Value FROM Employees WHERE IsActive = 1 AND DepartmentID = {0}", myDeptId)
                .AsEnumerable()
                .FirstOrDefault();

            // 2. SORGU: Benim Yüklediðim Doküman Sayýsý
            model.MyDocumentCount = _context.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) as Value FROM Documents WHERE OwnerEmployeeID = {0}", employeeId)
                .AsEnumerable()
                .FirstOrDefault();

            // 3. SORGU: Sistem genelinde Onay Bekleyen (Pending) Doküman Sayýsý
            model.PendingApprovalCount = _context.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) as Value FROM Documents WHERE CurrentStatus = 'Pending'")
                .AsEnumerable()
                .FirstOrDefault();

            // 4. SORGU: Son Eklenen 5 Doküman (Özet Liste)
            string recentDocsSql = @"
                SELECT TOP 5 
                    d.DocumentID, 
                    d.Title, 
                    (e.FirstName + ' ' + e.LastName) as OwnerName, 
                    d.CreatedDate, 
                    d.CurrentStatus as Status
                FROM Documents d
                JOIN Employees e ON d.OwnerEmployeeID = e.EmployeeID
                ORDER BY d.CreatedDate DESC";

            model.RecentDocuments = _context.Database
                .SqlQueryRaw<RecentDocumentViewModel>(recentDocsSql)
                .ToList();

            return View(model);
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}