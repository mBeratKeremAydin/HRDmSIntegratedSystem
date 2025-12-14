using HRDms.Data.Context;
using HR.Mvc.Models;
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
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId != null)
            {
                // Kullanıcının hala aktif olup olmadığını SQL ile kontrol et
                var sqlCheck = "SELECT COUNT(*) AS Value FROM Users WHERE UserID = {0} AND IsActive = 1";
                var isActive = _context.Database.SqlQueryRaw<int>(sqlCheck, userId).AsEnumerable().FirstOrDefault() > 0;

                if (isActive)
                {
                    var role = HttpContext.Session.GetString("UserRole");
                    return RedirectByRole(role);
                }
                else
                {
                    // Kullanıcı pasif veya silinmişse oturumu kapat
                    HttpContext.Session.Clear();
                }
            }

            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            var sql = @"
                SELECT 
                    u.UserID, 
                    u.Username, 
                    u.Email, 
                    e.EmployeeID, 
                    e.FirstName, 
                    e.LastName, 
                    r.RoleName
                FROM Users u
                LEFT JOIN Employees e ON u.UserID = e.UserID
                LEFT JOIN UserRoles ur ON u.UserID = ur.UserID
                LEFT JOIN Roles r ON ur.RoleID = r.RoleID
                WHERE u.Username = {0} AND u.UserPassword = {1} AND u.IsActive = 1";

            var loginData = _context.Database.SqlQueryRaw<UserLoginDTO>(sql, model.Username, model.Password).ToList();

            if (!loginData.Any())
            {
                TempData["ErrorMessage"] = "Kullanıcı adı veya şifre hatalı!";
                return View("Index", model);
            }

            var user = loginData.First();

            // Session'a kullanıcı bilgilerini kaydet
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("UserEmail", user.Email ?? "");

            // Kullanıcının tüm rollerini al
            var userRoles = loginData
                .Select(x => x.RoleName)
                .Where(r => !string.IsNullOrEmpty(r))
                .Cast<string>()
                .Distinct()
                .ToList();

            if (!userRoles.Any())
            {
                TempData["ErrorMessage"] = "Kullanıcınıza uygun bir rol tanımlanmamış!";
                return View("Index", model);
            }

            // Employee bilgisi varsa session'a ekle
            if (user.EmployeeId.HasValue)
            {
                HttpContext.Session.SetInt32("EmployeeId", user.EmployeeId.Value);
                HttpContext.Session.SetString("EmployeeName", $"{user.FirstName} {user.LastName}");
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
            
            if (roles.Contains("Department Manager"))
                return "Department Manager";
            
            if (roles.Contains("Employee"))
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

    public class UserLoginDTO
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string? Email { get; set; }
        public int? EmployeeId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? RoleName { get; set; }
    }
}