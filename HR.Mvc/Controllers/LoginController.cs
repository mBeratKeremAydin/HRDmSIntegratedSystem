using HRDms.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HR.Mvc.Controllers
{
    public class LoginController : Controller
    {
        private readonly AppDbContext _context;

        public LoginController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Zaten giriş yapmışsa ana sayfaya yönlendir
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                var role = HttpContext.Session.GetString("UserRole");
                return RedirectByRole(role);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                TempData["ErrorMessage"] = "Kullanıcı adı ve şifre gereklidir!";
                return RedirectToAction("Index");
            }

            // Kullanıcıyı bul (Tüm rolleri ile birlikte)
            var user = _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.Employee)
                .FirstOrDefault(u => u.Username == username && u.UserPassword == password && u.IsActive);

            if (user == null)
            {
                TempData["ErrorMessage"] = "Kullanıcı adı veya şifre hatalı!";
                return RedirectToAction("Index");
            }

            // Session'a kullanıcı bilgilerini kaydet
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("UserEmail", user.Email ?? "");

            // Kullanıcının tüm rollerini al
            var userRoles = user.UserRoles.Select(ur => ur.Role?.RoleName).Where(r => r != null).ToList();

            if (!userRoles.Any())
            {
                TempData["ErrorMessage"] = "Kullanıcınıza uygun bir rol tanımlanmamış!";
                return RedirectToAction("Index");
            }

            // Employee bilgisi varsa session'a ekle
            if (user.Employee != null)
            {
                HttpContext.Session.SetInt32("EmployeeId", user.Employee.EmployeeId);
                HttpContext.Session.SetString("EmployeeName", $"{user.Employee.FirstName} {user.Employee.LastName}");
            }

            // Rol önceliği belirle ve yönlendir
            string primaryRole = DeterminePrimaryRole(userRoles);
            HttpContext.Session.SetString("UserRole", primaryRole);

            // Tüm rolleri virgülle ayrılmış string olarak kaydet (yetki kontrolü için)
            HttpContext.Session.SetString("UserRoles", string.Join(",", userRoles));

            TempData["SuccessMessage"] = $"Hoş geldiniz, {user.Username}!";


            return RedirectByRole(primaryRole);
        }

        /// <summary>
        /// Kullanıcının rollerine göre öncelik sırasına göre ana rolü belirler
        /// Öncelik: Admin > HR > DepartmentManager > Employee
        /// </summary>
        private string DeterminePrimaryRole(List<string> roles)
        {
            // Rol öncelik sırası
            if (roles.Contains("Admin"))
                return "Admin";
            
            if (roles.Contains("HR"))
                return "HR";
            
            if (roles.Contains("Department Manager") || roles.Contains("Employee"))
                return "Department Manager";
            
            if (roles.Contains("Employee") && !roles.Contains("Department Manager"))
                return "Employee";

            // Varsayılan
            return roles.FirstOrDefault() ?? "Employee";
        }

        /// <summary>
        /// Role göre ilgili controller'a yönlendirir
        /// </summary>
        private IActionResult RedirectByRole(string role)
        {
            return role switch
            {
                "Admin" => RedirectToAction("Index", "Admin"), 
                "HR" => RedirectToAction("Index", "HR"),
                "Department Manager" => RedirectToAction("Index", "Department"), // DepartmentManager kendi ekranına
                "DepManager" => RedirectToAction("Index", "Department"),
                "Employee" => RedirectToAction("Index", "Employee"),
                _ => RedirectToAction("Index", "Employee") // Varsayılan
            };
        }

        /// <summary>
        /// Kullanıcının belirli bir role sahip olup olmadığını kontrol eder
        /// </summary>
        public bool HasRole(string roleName)
        {
            var userRoles = HttpContext.Session.GetString("UserRoles");
            if (string.IsNullOrEmpty(userRoles))
                return false;

            return userRoles.Split(',').Contains(roleName);
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["SuccessMessage"] = "Başarıyla çıkış yaptınız.";
            return RedirectToAction("Index");
        }
    }
}