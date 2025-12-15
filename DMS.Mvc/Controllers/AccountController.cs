using HRDms.Data.Context;
using HRDms.Data.Models; 
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DMS.Mvc.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Login Sayfasını Gösterir
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // POST: Login Butonuna basılınca çalışır
        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            // 1. KULLANICIYI BUL
            string sqlQuery = "SELECT * FROM Users WHERE Username = {0} AND UserPassword = {1}";
            var user = _context.Users.FromSqlRaw(sqlQuery, username, password).AsEnumerable().FirstOrDefault();

            if (user != null)
            {
                // 2. PERSONEL BİLGİSİNİ BUL
                string empSql = "SELECT * FROM Employees WHERE UserID = {0}";
                var employee = _context.Employees.FromSqlRaw(empSql, user.UserId).AsEnumerable().FirstOrDefault();

                if (employee != null)
                {
                    // Temel Session Bilgilerini Doldur
                    HttpContext.Session.SetInt32("UserID", user.UserId);
                    HttpContext.Session.SetInt32("EmployeeID", employee.EmployeeId);
                    HttpContext.Session.SetInt32("DepartmentID", employee.DepartmentId);
                    HttpContext.Session.SetString("Username", user.Username);

                    // YETKİ KONTROLÜ (ADMIN ve MANAGER) ---

                    // A) Önce Rolüne Bakalım (Admin mi?)
                    string roleSql = @"
                        SELECT r.RoleName 
                        FROM Roles r 
                        JOIN UserRoles ur ON r.RoleID = ur.RoleID 
                        WHERE ur.UserID = {0}";

                    var roleName = _context.Database
                                           .SqlQueryRaw<string>(roleSql, user.UserId)
                                           .AsEnumerable()
                                           .FirstOrDefault();

                    if (roleName == "Admin")
                    {
                        // EĞER ADMIN İSE: Hem Admin hem Manager yetkisi ver
                        HttpContext.Session.SetString("IsAdmin", "true");
                        HttpContext.Session.SetString("IsManager", "true");
                    }
                    else
                    {
                        // EĞER ADMIN DEĞİLSE:
                        HttpContext.Session.SetString("IsAdmin", "false");

                        // B) O zaman Departman Yöneticisi mi diye bak
                        string managerCheckSql = "SELECT COUNT(*) as Value FROM Departments WHERE ManagerID = {0}";

                        int managerCount = _context.Database
                            .SqlQueryRaw<int>(managerCheckSql, employee.EmployeeId)
                            .AsEnumerable()
                            .FirstOrDefault();

                        if (managerCount > 0)
                        {
                            HttpContext.Session.SetString("IsManager", "true");
                        }
                        else
                        {
                            HttpContext.Session.SetString("IsManager", "false");
                        }
                    }
                    // --- YETKİ KONTROLÜ BİTİŞ ---

                    return RedirectToAction("Index", "Home");
                }
            }

            ViewBag.Error = "Kullanıcı adı veya şifre hatalı!";
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // Oturumu temizle
            return RedirectToAction("Login");
        }
    }
}