using HRDms.Data.Context;
using HRDms.Data.Context;
using HRDms.Data.Models;
using DMS.Mvc.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DMS.Mvc.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // Admin Yetki Kontrolü (Her metodun başında tekrar tekrar yazmamak için)
        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("IsAdmin") == "true";
        }

        // 1. KATEGORİ YÖNETİMİ
        public IActionResult Categories()
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            var categories = _context.DocumentCategories
                .FromSqlRaw("SELECT * FROM DocumentCategories")
                .ToList();

            return View(categories);
        }

        [HttpPost]
        public IActionResult CreateCategory(string categoryName)
        {
            if (!IsAdmin()) return Unauthorized();

            if (!string.IsNullOrEmpty(categoryName))
            {
                string sql = "INSERT INTO DocumentCategories (CategoryName) VALUES ({0})";
                _context.Database.ExecuteSqlRaw(sql, categoryName);
            }
            return RedirectToAction("Categories");
        }

        [HttpPost]
        public IActionResult DeleteCategory(int id)
        {
            if (!IsAdmin()) return Unauthorized();

            // 1. KONTROL: Bu kategoride hiç dosya var mı?
            string checkSql = "SELECT COUNT(*) as Value FROM Documents WHERE CategoryID = {0} AND IsActive = 1";
            int fileCount = _context.Database
                                    .SqlQueryRaw<int>(checkSql, id)
                                    .AsEnumerable()
                                    .FirstOrDefault();

            if (fileCount > 0)
            {
                // Eğer dosya varsa silme, hata mesajı gönder.
                TempData["ErrorMessage"] = $"Bu kategoride {fileCount} adet dosya var. Önce onları taşımalı veya silmelisiniz.";
                return RedirectToAction("Categories");
            }

            // 2. KONTROL: Bu kategorinin alt kategorileri var mı? (ParentCategoryID)
            string childCheckSql = "SELECT COUNT(*) as Value FROM DocumentCategories WHERE ParentCategoryID = {0}";
            int childCount = _context.Database
                                     .SqlQueryRaw<int>(childCheckSql, id)
                                     .AsEnumerable()
                                     .FirstOrDefault();

            if (childCount > 0)
            {
                TempData["ErrorMessage"] = "Bu kategorinin altında başka alt kategoriler var. Önce onları silmelisiniz.";
                return RedirectToAction("Categories");
            }

            // 3. SİLME İŞLEMİ (Engel yoksa sil)
            try
            {
                string deleteSql = "DELETE FROM DocumentCategories WHERE CategoryID = {0}";
                _context.Database.ExecuteSqlRaw(deleteSql, id);
                TempData["SuccessMessage"] = "Kategori başarıyla silindi.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Silme işlemi sırasında veritabanı hatası oluştu: " + ex.Message;
            }

            return RedirectToAction("Categories");
        }

        // 2. SİSTEM LOGLARI (AUDIT)
        public IActionResult SystemLogs()
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home");

            // Tüm geçmişi çekiyoruz (Doküman adı ve Kullanıcı adıyla beraber)
            string sql = @"
                SELECT 
                    h.Status, 
                    u.Username as ChangedByName, 
                    h.ChangeDate,
                    d.Title as DocumentTitle -- Dokümanın adını da görmek isteriz
                FROM DocumentStatusHistory h
                LEFT JOIN Users u ON h.ChangedByUserID = u.UserID
                JOIN Documents d ON h.DocumentID = d.DocumentID
                ORDER BY h.ChangeDate DESC";


            var logs = _context.Database
                               .SqlQueryRaw<SystemLogViewModel>(sql)
                               .ToList();

            return View(logs);
        }
    }
}