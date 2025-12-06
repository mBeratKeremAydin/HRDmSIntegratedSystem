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
    }
}
