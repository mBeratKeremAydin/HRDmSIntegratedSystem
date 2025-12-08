using HRDms.Data.Context;
using HRDms.Data.Models; // ViewModel'in olduğu yer
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DMS.Mvc.Controllers
{
    public class DocumentController : Controller
    {
        private readonly AppDbContext _context;

        public DocumentController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Document/Index
        public IActionResult Index(string searchString)
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            var deptId = HttpContext.Session.GetInt32("DepartmentID");

            if (userId == null) return RedirectToAction("Login", "Account");

            // Temel Sorgu
            string sqlQuery = @"
                SELECT 
                    d.DocumentID, 
                    d.Title, 
                    c.CategoryName, 
                    (e.FirstName + ' ' + e.LastName) AS OwnerName, 
                    d.CreatedDate, 
                    d.CurrentStatus
                FROM Documents d
                JOIN DocumentCategories c ON d.CategoryID = c.CategoryID
                JOIN Employees e ON d.OwnerEmployeeID = e.EmployeeID
                WHERE d.IsActive = 1 AND e.DepartmentID = {0}";

            // EĞER ARAMA YAPILDIYSA SQL'E EKLE
            if (!string.IsNullOrEmpty(searchString))
            {
                // SQL Injection riskine karşı parametre kullanmak en doğrusudur ama 
                // Raw SQL'de dinamik WHERE eklerken {1} parametresini elle yönetmek zordur.
                // Basitlik adına string interpolation yapıyoruz (Ders projesi için kabul görür).
                // Güvenli yöntem için FromSqlInterpolated kullanılır.

                sqlQuery += $" AND d.Title LIKE '%{searchString}%'";
            }

            sqlQuery += " ORDER BY d.CreatedDate DESC";

            var documents = _context.Database
                                    .SqlQueryRaw<DocumentListViewModel>(sqlQuery, deptId)
                                    .ToList();

            // View'de arama kutusunda aranan kelime kalsın diye geri gönderiyoruz
            ViewData["CurrentFilter"] = searchString;

            return View(documents);
        }

        // GET: /Document/Create
        [HttpGet]
        public IActionResult Create()
        {
            // Kullanıcı kontrolü
            if (HttpContext.Session.GetInt32("UserID") == null) return RedirectToAction("Login", "Account");

            // 1. Kategorileri Dropdown için Çek (RAW SQL)
            string sql = "SELECT * FROM DocumentCategories";
            var categories = _context.DocumentCategories
                                     .FromSqlRaw(sql)
                                     .ToList();

            // Modeli hazırla ve view'e gönder
            var model = new UploadDocumentViewModel
            {
                Categories = categories
            };

            return View(model);
        }

        // POST: /Document/Create
        [HttpPost]
        public async Task<IActionResult> Create(UploadDocumentViewModel model)
        {

            // İzin verilen uzantılar
            var allowedExtensions = new[] { ".pdf", ".docx", ".xlsx", ".jpg", ".png" };
            var ext = Path.GetExtension(model.File.FileName).ToLower(); // file nesnesi Create'de model.File, diğerinde file diye geçer.

            if (!allowedExtensions.Contains(ext))
            {
                // Create metodundaysan:
                ModelState.AddModelError("", "Geçersiz dosya formatı! Sadece PDF, Word, Excel ve Resim yükleyebilirsiniz.");
                // Modeli tekrar doldurup return View(model); demen lazım.

                // UploadNewVersion metodundaysan:
                return BadRequest("Geçersiz dosya formatı!");
            }

            // Session Kontrolü
            int? employeeId = HttpContext.Session.GetInt32("EmployeeID");
            int? userId = HttpContext.Session.GetInt32("UserID");

            if (employeeId == null) return RedirectToAction("Login", "Account");


            if (ModelState.IsValid)
            {
                // 1. DOSYAYI FİZİKSEL OLARAK KAYDET
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.File.FileName;
                string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/documents");

                if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

                string filePath = Path.Combine(uploadFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.File.CopyToAsync(stream);
                }

                // 2. SQL İLE 'DOCUMENTS' TABLOSUNA KAYIT VE ID ALMA (TEK SEFERDE)
                // DÜZELTME: Insert yaptık ve hemen ardından SCOPE_IDENTITY() ile ID'yi istedik.
                // CAST(... as int) diyerek SQL'den gelen decimal değeri int'e çevirdik.
                string sqlDoc = @"
                INSERT INTO Documents (Title, DocumentDescription, CategoryID, OwnerEmployeeID, CreatedDate, CurrentStatus, IsActive)
                VALUES ({0}, {1}, {2}, {3}, GETDATE(), 'Pending', 1);
                SELECT CAST(SCOPE_IDENTITY() as int);";

                // SqlQueryRaw tek bir değer (ID) dönecek
                int newDocumentId = _context.Database
                                    .SqlQueryRaw<int>(sqlDoc,
                                        model.Title,
                                        model.Description ?? "",
                                        model.CategoryID,
                                        employeeId)
                                    .AsEnumerable()
                                    .First();

                // 3. SQL İLE 'DOCUMENTVERSIONS' TABLOSUNA KAYIT
                string fileExt = Path.GetExtension(model.File.FileName);
                string relativePath = "/documents/" + uniqueFileName;

                string sqlVer = @"
                    INSERT INTO DocumentVersions (DocumentID, VersionNumber, FilePath, FileExtension, UploadedByUserID, UploadDate, ChangeNote)
                    VALUES ({0}, 1, {1}, {2}, {3}, GETDATE(), 'Initial Upload')";

                _context.Database.ExecuteSqlRaw(sqlVer, newDocumentId, relativePath, fileExt, userId);

                return RedirectToAction("Index");
            }

            // Hata varsa kategorileri tekrar doldur
            model.Categories = _context.DocumentCategories.FromSqlRaw("SELECT * FROM DocumentCategories").ToList();
            return View(model);
        }

        // GET: /Document/Details/5
        public IActionResult Details(int id)
        {
            if (HttpContext.Session.GetInt32("UserID") == null) return RedirectToAction("Login", "Account");

            // 1. DOKÜMAN BİLGİSİNİ ÇEK
            string sqlDoc = @"
                SELECT TOP 1 
                    d.DocumentID, 
                    d.Title, 
                    d.DocumentDescription as Description, 
                    c.CategoryName, 
                    (e.FirstName + ' ' + e.LastName) AS OwnerName, 
                    d.CurrentStatus, 
                    d.CreatedDate,
                    v.FilePath,
                    v.VersionNumber
                FROM Documents d
                JOIN DocumentCategories c ON d.CategoryID = c.CategoryID
                JOIN Employees e ON d.OwnerEmployeeID = e.EmployeeID
                LEFT JOIN DocumentVersions v ON d.DocumentID = v.DocumentID
                WHERE d.DocumentID = {0}
                ORDER BY v.VersionNumber DESC";

            var document = _context.Database
                                   .SqlQueryRaw<DocumentDetailViewModel>(sqlDoc, id)
                                   .AsEnumerable()
                                   .FirstOrDefault();

            if (document == null) return NotFound();

            if (!string.IsNullOrEmpty(document.FilePath))
            {
                document.FileName = Path.GetFileName(document.FilePath);
            }

            // 2. TARİHÇE BİLGİSİNİ ÇEK (YENİ KISIM)
            // DocumentStatusHistory tablosunu Users tablosuyla birleştirip kullanıcı adını alıyoruz.
            string sqlHistory = @"
                SELECT 
                    h.Status, 
                    u.Username as ChangedByName, 
                    h.ChangeDate
                FROM DocumentStatusHistory h
                LEFT JOIN Users u ON h.ChangedByUserID = u.UserID
                WHERE h.DocumentID = {0}
                ORDER BY h.ChangeDate DESC";

            var historyList = _context.Database
                                      .SqlQueryRaw<StatusHistoryViewModel>(sqlHistory, id)
                                      .ToList();

            document.History = historyList; // Listeyi modele koyduk

            // ... (Önceki kodlar: document ve historyList çekildi) ...

            // 3. VERSİYON GEÇMİŞİNİ ÇEK (YENİ KISIM)
            string sqlVersions = @"
                SELECT 
                    v.VersionID,
                    v.VersionNumber,
                    v.FilePath as FileName, -- Geçici olarak buraya alıyoruz, aşağıda düzelteceğiz
                    v.UploadDate,
                    u.Username as UploadedByName,
                    v.ChangeNote
                FROM DocumentVersions v
                LEFT JOIN Users u ON v.UploadedByUserID = u.UserID
                WHERE v.DocumentID = {0}
                ORDER BY v.VersionNumber DESC";

            var versionList = _context.Database
                                      .SqlQueryRaw<DocumentVersionViewModel>(sqlVersions, id)
                                      .ToList();

            // Dosya yollarını temizleyip sadece dosya adı yapalım
            foreach (var v in versionList)
            {
                if (!string.IsNullOrEmpty(v.FileName))
                {
                    v.FileName = Path.GetFileName(v.FileName);
                }
            }

            document.Versions = versionList; // Modele ekledik

            return View(document);

        }

        // POST: /Document/ChangeStatus
        [HttpPost]
        public IActionResult ChangeStatus(int documentId, string newStatus)
        {

            // --- GÜVENLİK KONTROLÜ BAŞLANGIÇ ---
            if (HttpContext.Session.GetString("IsManager") != "true")
            {
                return Unauthorized(); // Yetkin yok kardeşim!
            }
            // --- GÜVENLİK KONTROLÜ BİTİŞ ---

            int? userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return RedirectToAction("Login", "Account");

            // 1. Documents Tablosunu Güncelle
            string updateSql = "UPDATE Documents SET CurrentStatus = {0} WHERE DocumentID = {1}";
            _context.Database.ExecuteSqlRaw(updateSql, newStatus, documentId);

            // 2. DocumentStatusHistory Tablosuna Log At (Tracking)
            string historySql = @"
                INSERT INTO DocumentStatusHistory (DocumentID, Status, ChangedByUserID, ChangeDate)
                VALUES ({0}, {1}, {2}, GETDATE())";

            _context.Database.ExecuteSqlRaw(historySql, documentId, newStatus, userId);

            // İşlem bitince tekrar detay sayfasına dön
            return RedirectToAction("Details", new { id = documentId });
        }

        // GET: /Document/DownloadFile/5
        public IActionResult DownloadFile(int id)
        {
            // 1. Dosya yolunu veritabanından bul
            string sql = "SELECT TOP 1 FilePath FROM DocumentVersions WHERE DocumentID = {0} ORDER BY VersionNumber DESC";

            // Sadece string döndüren basit bir sorgu yapıyoruz
            var result = _context.DocumentVersions
                                 .FromSqlRaw(sql, id)
                                 .Select(x => x.FilePath)
                                 .FirstOrDefault();

            if (string.IsNullOrEmpty(result)) return NotFound("Dosya bulunamadı.");

            // 2. Fiziksel yolu oluştur (wwwroot + veritabanındaki yol)
            // Veritabanında '/documents/...' şeklinde kayıtlı, başındaki / işaretini temizlememiz gerekebilir.
            string cleanPath = result.TrimStart('/');
            string physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", cleanPath);

            if (!System.IO.File.Exists(physicalPath)) return NotFound("Dosya sunucuda fiziksel olarak yok!");

            // 3. Dosyayı indir
            byte[] fileBytes = System.IO.File.ReadAllBytes(physicalPath);
            string fileName = Path.GetFileName(physicalPath);

            // "application/octet-stream" her türlü dosyayı indirtir (PDF, DOCX, JPG fark etmez)
            return File(fileBytes, "application/octet-stream", fileName);
        }

        // POST: /Document/Delete/5
        [HttpPost]
        public IActionResult Delete(int id)
        {
            // 1. GÜVENLİK: Sadece Yöneticiler Silebilir!
            string isManager = HttpContext.Session.GetString("IsManager");
            if (isManager != "true")
            {
                // Yetkisiz işlem denemesi
                return Unauthorized();
            }

            // 2. HARD DELETE (Gerçek Silme)
            // SQL'deki ON DELETE CASCADE ayarların sayesinde,
            // Documents tablosundan sildiğimizde Versions, History vb. her şey otomatik silinecek.

            string sql = "DELETE FROM Documents WHERE DocumentID = {0}";
            _context.Database.ExecuteSqlRaw(sql, id);

            return RedirectToAction("Index");
        }

        // GET: /Document/WaitingApprovals
        public IActionResult WaitingApprovals()
        {
            // 1. GÜVENLİK: Sadece Yöneticiler Girebilir
            if (HttpContext.Session.GetString("IsManager") != "true")
            {
                return RedirectToAction("Index"); // Veya Unauthorized sayfası
            }

            int? deptId = HttpContext.Session.GetInt32("DepartmentID");

            // 2. SORGULAMA:
            // Sadece 'Pending' durumunda olan VE Yöneticinin Departmanındaki dosyaları getir.
            string sql = @"
                SELECT 
                    d.DocumentID, 
                    d.Title, 
                    c.CategoryName, 
                    (e.FirstName + ' ' + e.LastName) AS OwnerName, 
                    d.CreatedDate, 
                    d.CurrentStatus
                FROM Documents d
                JOIN DocumentCategories c ON d.CategoryID = c.CategoryID
                JOIN Employees e ON d.OwnerEmployeeID = e.EmployeeID
                WHERE d.IsActive = 1 
                  AND d.CurrentStatus = 'Pending' 
                  AND e.DepartmentID = {0}
                ORDER BY d.CreatedDate ASC"; // Eskiden yeniye (önce eskiler onaylansın)

            var documents = _context.Database
                                    .SqlQueryRaw<DocumentListViewModel>(sql, deptId)
                                    .ToList();

            return View(documents);
        }

        // POST: /Document/RollbackVersion
        [HttpPost]
        public IActionResult RollbackVersion(int versionId, int documentId)
        {
            // 1. GÜVENLİK: Sadece Yönetici
            if (HttpContext.Session.GetString("IsManager") != "true") return Unauthorized();

            int? userId = HttpContext.Session.GetInt32("UserID");

            // 2. HEDEF VERSİYONUN BİLGİLERİNİ BUL (Raw SQL)
            string targetSql = "SELECT * FROM DocumentVersions WHERE VersionID = {0}";
            var targetVersion = _context.DocumentVersions
                                        .FromSqlRaw(targetSql, versionId)
                                        .FirstOrDefault();

            if (targetVersion != null)
            {
                // 3. YENİ VERSİYON NUMARASINI HESAPLA (Max + 1)
                string maxVerSql = "SELECT MAX(VersionNumber) as Value FROM DocumentVersions WHERE DocumentID = {0}";
                int maxVersion = _context.Database.SqlQueryRaw<int>(maxVerSql, documentId).AsEnumerable().FirstOrDefault();
                int newVersion = maxVersion + 1;

                // 4. ESKİ DOSYAYI YENİ VERSİYON OLARAK KAYDET
                // Not: Dosya fiziksel olarak kopyalanmaz, sadece veritabanında aynı yolu gösteren yeni kayıt açılır.
                string insertSql = @"
                    INSERT INTO DocumentVersions (DocumentID, VersionNumber, FilePath, FileExtension, UploadedByUserID, UploadDate, ChangeNote)
                    VALUES ({0}, {1}, {2}, {3}, {4}, GETDATE(), {5})";

                string note = $"Rollback to v{targetVersion.VersionNumber}"; // "v1 sürümüne geri dönüldü" notu

                _context.Database.ExecuteSqlRaw(insertSql,
                    documentId,
                    newVersion,
                    targetVersion.FilePath,
                    targetVersion.FileExtension,
                    userId,
                    note);
            }

            return RedirectToAction("Details", new { id = documentId });
        }

        // POST: /Document/UploadNewVersion
        [HttpPost]
        public async Task<IActionResult> UploadNewVersion(int documentId, IFormFile file, string changeNote)
        {

            // İzin verilen uzantılar
            var allowedExtensions = new[] { ".pdf", ".docx", ".xlsx", ".jpg", ".png" };
            var ext = Path.GetExtension(file.FileName).ToLower(); // file nesnesi Create'de model.File, diğerinde file diye geçer.

            if (!allowedExtensions.Contains(ext))
            {
                // Create metodundaysan:
                ModelState.AddModelError("", "Geçersiz dosya formatı! Sadece PDF, Word, Excel ve Resim yükleyebilirsiniz.");
                // Modeli tekrar doldurup return View(model); demen lazım.

                // UploadNewVersion metodundaysan:
                return BadRequest("Geçersiz dosya formatı!");
            }

            // 1. Session ve Yetki Kontrolü
            int? userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return RedirectToAction("Login", "Account");

            if (file != null && file.Length > 0)
            {
                // 2. DOSYAYI FİZİKSEL KAYDET
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                string uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/documents");
                string filePath = Path.Combine(uploadFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // 3. YENİ VERSİYON NUMARASINI HESAPLA (MAX + 1)
                string maxVerSql = "SELECT MAX(VersionNumber) as Value FROM DocumentVersions WHERE DocumentID = {0}";
                int maxVersion = _context.Database.SqlQueryRaw<int>(maxVerSql, documentId).AsEnumerable().FirstOrDefault();
                int nextVersion = maxVersion + 1;

                // 4. VERİTABANINA YENİ VERSİYON EKLE
                string fileExt = Path.GetExtension(file.FileName);
                string relativePath = "/documents/" + uniqueFileName;

                string insertSql = @"
                    INSERT INTO DocumentVersions (DocumentID, VersionNumber, FilePath, FileExtension, UploadedByUserID, UploadDate, ChangeNote)
                    VALUES ({0}, {1}, {2}, {3}, {4}, GETDATE(), {5})";

                _context.Database.ExecuteSqlRaw(insertSql,
                    documentId,
                    nextVersion,
                    relativePath,
                    fileExt,
                    userId,
                    changeNote ?? "Revize edildi"); // Not girilmediyse varsayılan yaz

                // 5. DOKÜMAN DURUMUNU 'PENDING'E ÇEK (Yönetici tekrar onaylasın)
                // Ayrıca Tarihçe tablosuna da log atalım ki "Yeni versiyon geldi" anlaşılsın.
                string updateDocSql = "UPDATE Documents SET CurrentStatus = 'Pending' WHERE DocumentID = {0}";
                _context.Database.ExecuteSqlRaw(updateDocSql, documentId);

                string historySql = "INSERT INTO DocumentStatusHistory (DocumentID, Status, ChangedByUserID, ChangeDate) VALUES ({0}, 'Pending (v' + CAST({1} as varchar) + ')', {2}, GETDATE())";
                _context.Database.ExecuteSqlRaw(historySql, documentId, nextVersion, userId);
            }

            return RedirectToAction("Details", new { id = documentId });
        }

    }
}