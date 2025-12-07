using HRDms.Data.Context;
using HRDms.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HR.Mvc.Controllers
{
    public class LeaveRequestsController : Controller
    {
        private readonly AppDbContext _context;

        public LeaveRequestsController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(string? status = null, int? employeeId = null)
        {
            // Tüm izin taleplerini getir (İlişkili verilerle birlikte)
            var query = _context.LeaveRequests
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Department)
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Job)
                .Include(l => l.LeaveType)
                .Include(l => l.ApprovedByUser)
                .AsQueryable();

            // Status filtreleme
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(l => l.Status == status);
                ViewBag.CurrentStatus = status;
            }

            // Employee filtreleme
            if (employeeId.HasValue)
            {
                query = query.Where(l => l.EmployeeId == employeeId.Value);
                ViewBag.CurrentEmployeeId = employeeId;
            }

            var leaveRequests = query
                .OrderByDescending(l => l.StartDate)
                .ToList();

            // İstatistikler için
            ViewBag.TotalRequests = _context.LeaveRequests.Count();
            ViewBag.PendingRequests = _context.LeaveRequests.Count(l => l.Status == "Pending");
            ViewBag.ApprovedRequests = _context.LeaveRequests.Count(l => l.Status == "Approved");
            ViewBag.RejectedRequests = _context.LeaveRequests.Count(l => l.Status == "Rejected");

            // Çalışan listesi (filtreleme için)
            ViewBag.Employees = _context.Employees
                .Where(e => e.IsActive)
                .Select(e => new { e.EmployeeId, FullName = e.FirstName + " " + e.LastName })
                .ToList();

            return View(leaveRequests);
        }

        // Onaylama
        [HttpPost]
        public IActionResult Approve(int id)
        {
            var leave = _context.LeaveRequests.Find(id);
            if (leave != null)
            {
                leave.Status = "Approved";
                
                // Session'dan giriş yapan kullanıcının ID'sini al
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId.HasValue)
                {
                    leave.ApprovedByUserId = userId.Value;
                }
                
                _context.SaveChanges();
                TempData["SuccessMessage"] = "İzin talebi onaylandı.";
            }
            return RedirectToAction("Index");
        }

        // Reddetme
        [HttpPost]
        public IActionResult Reject(int id)
        {
            var leave = _context.LeaveRequests.Find(id);
            if (leave != null)
            {
                leave.Status = "Rejected";
                
                // Session'dan giriş yapan kullanıcının ID'sini al
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId.HasValue)
                {
                    leave.ApprovedByUserId = userId.Value;
                }
                
                _context.SaveChanges();
                TempData["SuccessMessage"] = "İzin talebi reddedildi.";
            }
            return RedirectToAction("Index");
        }

        // Detay sayfası
        public IActionResult Details(int id)
        {
            var leave = _context.LeaveRequests
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Department)
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Job)
                .Include(l => l.LeaveType)
                .Include(l => l.ApprovedByUser)
                .FirstOrDefault(l => l.RequestId == id);

            if (leave == null)
                return NotFound();

            return View(leave);
        }
        // İzin Talebi Oluşturma - GET
        [HttpGet]
        public IActionResult Create()
        {
            // Session kontrolü
            var userId = HttpContext.Session.GetInt32("UserId");
            var employeeId = HttpContext.Session.GetInt32("EmployeeId");

            if (userId == null || employeeId == null)
            {
                TempData["ErrorMessage"] = "Oturum bilgisi bulunamadı. Lütfen tekrar giriş yapın.";
                return RedirectToAction("Index", "Login");
            }

            // Çalışan bilgisini getir
            var employee = _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Job)
                .FirstOrDefault(e => e.EmployeeId == employeeId);

            if (employee == null)
            {
                TempData["ErrorMessage"] = "Çalışan bilgisi bulunamadı!";
                return RedirectToAction("Index", "Employee");
            }

            // İzin türlerini getir
            ViewBag.LeaveTypes = _context.LeaveTypes
                .Select(lt => new { lt.LeaveTypeId, lt.TypeName, lt.DaysAllowed })
                .ToList();

            // Çalışan bilgisini ViewBag'e ekle
            ViewBag.Employee = employee;
            ViewBag.EmployeeId = employeeId;

            return View();
        }

        // İzin Talebi Oluşturma - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(int leaveTypeId, DateOnly startDate, DateOnly endDate, string? reason)
        {
            try
            {
                // Session kontrolü
                var employeeId = HttpContext.Session.GetInt32("EmployeeId");

                if (employeeId == null)
                {
                    TempData["ErrorMessage"] = "Oturum bilgisi bulunamadı!";
                    return RedirectToAction("Index", "Login");
                }

                // Tarih kontrolü
                if (startDate >= endDate)
                {
                    TempData["ErrorMessage"] = "Bitiş tarihi, başlangıç tarihinden sonra olmalıdır!";
                    ReloadCreateDropdowns(employeeId.Value);
                    return View();
                }

                // Geçmiş tarih kontrolü
                if (startDate < DateOnly.FromDateTime(DateTime.Now))
                {
                    TempData["ErrorMessage"] = "Geçmiş tarih için izin talebi oluşturamazsınız!";
                    ReloadCreateDropdowns(employeeId.Value);
                    return View();
                }

                // İzin talebini oluştur
                var leaveRequest = new LeaveRequest
                {
                    EmployeeId = employeeId.Value,
                    LeaveTypeId = leaveTypeId,
                    StartDate = startDate,
                    EndDate = endDate,
                    Reason = reason,
                    Status = "Pending" // Varsayılan olarak beklemede
                };

                _context.LeaveRequests.Add(leaveRequest);
                _context.SaveChanges();

                TempData["SuccessMessage"] = "İzin talebiniz başarıyla oluşturuldu! Onay bekliyor.";
                return RedirectToAction("Index", "Employee");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Bir hata oluştu: {ex.Message}";
                ReloadCreateDropdowns(HttpContext.Session.GetInt32("EmployeeId").Value);
                return View();
            }
        }

        private void ReloadCreateDropdowns(int employeeId)
        {
            ViewBag.LeaveTypes = _context.LeaveTypes
                .Select(lt => new { lt.LeaveTypeId, lt.TypeName, lt.DaysAllowed })
                .ToList();

            ViewBag.Employee = _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Job)
                .FirstOrDefault(e => e.EmployeeId == employeeId);

            ViewBag.EmployeeId = employeeId;
        }
    }

    }

