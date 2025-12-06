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
                if (role == "HR")
                    return RedirectToAction("Index", "HR");
                else if (role == "Employee")
                    return RedirectToAction("Index", "Employee");
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

            // Kullanıcıyı bul
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

            // Rol kontrolü
            var userRole = user.UserRoles.FirstOrDefault()?.Role?.RoleName;

            if (!string.IsNullOrEmpty(userRole))
            {
                HttpContext.Session.SetString("UserRole", userRole);

                // Employee bilgisi varsa
                if (user.Employee != null)
                {
                    HttpContext.Session.SetInt32("EmployeeId", user.Employee.EmployeeId);
                    HttpContext.Session.SetString("EmployeeName", $"{user.Employee.FirstName} {user.Employee.LastName}");
                }

                TempData["SuccessMessage"] = $"Hoş geldiniz, {user.Username}!";

                // Role göre yönlendirme
                if (userRole == "HR" || userRole == "Admin")
                {
                    return RedirectToAction("Index", "HR");
                }
                else if (userRole == "Employee")
                {
                    return RedirectToAction("Index", "Employee");
                }
            }

            // Rol yoksa varsayılan olarak Employee ekranına yönlendir
            HttpContext.Session.SetString("UserRole", "Employee");

            if (user.Employee != null)
            {
                HttpContext.Session.SetInt32("EmployeeId", user.Employee.EmployeeId);
                return RedirectToAction("Index", "Employee");
            }

            TempData["ErrorMessage"] = "Kullanıcınıza uygun bir rol tanımlanmamış!";
            return RedirectToAction("Index");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["SuccessMessage"] = "Başarıyla çıkış yaptınız.";
            return RedirectToAction("Index");
        }
    }
}