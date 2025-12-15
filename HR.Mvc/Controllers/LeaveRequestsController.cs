using HRDms.Data.Context;
using HRDms.Data.Models;
using HR.Mvc.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

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
            //LeaveReq infos
            var sql = @"
                SELECT 
                    lr.RequestId,
                    lr.EmployeeId,
                    e.FirstName AS EmployeeFirstName,
                    e.LastName AS EmployeeLastName,
                    d.DepartmentName,
                    j.JobTitle,
                    e.Email,
                    lt.TypeName AS LeaveTypeName,
                    CAST(lr.StartDate AS datetime) AS StartDate,
                    CAST(lr.EndDate AS datetime) AS EndDate,
                    lr.Status,
                    lr.Reason,
                    lr.ApprovedByUserId,
                    u.Username AS ApprovedByUserName
                FROM LeaveRequests lr
                INNER JOIN Employees e ON lr.EmployeeId = e.EmployeeId
                LEFT JOIN Departments d ON e.DepartmentId = d.DepartmentId
                LEFT JOIN Jobs j ON e.JobId = j.JobId
                INNER JOIN LeaveTypes lt ON lr.LeaveTypeId = lt.LeaveTypeId
                LEFT JOIN Users u ON lr.ApprovedByUserId = u.UserId
                WHERE 1=1";

            var parameters = new List<object>();

            if (!string.IsNullOrEmpty(status))
            {
                sql += " AND lr.Status = {0}";
                parameters.Add(status);
            }

            if (employeeId.HasValue)
            {
                sql += " AND lr.EmployeeId = {" + parameters.Count + "}";
                parameters.Add(employeeId.Value);
            }

            sql += " ORDER BY lr.StartDate DESC";

            var leaveRequests = _context.Database.SqlQueryRaw<LeaveRequestViewModel>(sql, parameters.ToArray()).ToList();

            ViewBag.CurrentStatus = status;
            ViewBag.CurrentEmployeeId = employeeId;

            // İstatistikler
            ViewBag.TotalRequests = _context.Database.SqlQueryRaw<int>("SELECT COUNT(*) as Value FROM LeaveRequests").AsEnumerable().FirstOrDefault();
            ViewBag.PendingRequests = _context.Database.SqlQueryRaw<int>("SELECT COUNT(*) as Value FROM LeaveRequests WHERE Status = 'Pending'").AsEnumerable().FirstOrDefault();
            ViewBag.ApprovedRequests = _context.Database.SqlQueryRaw<int>("SELECT COUNT(*) as Value FROM LeaveRequests WHERE Status = 'Approved'").AsEnumerable().FirstOrDefault();
            ViewBag.RejectedRequests = _context.Database.SqlQueryRaw<int>("SELECT COUNT(*) as Value FROM LeaveRequests WHERE Status = 'Rejected'").AsEnumerable().FirstOrDefault();

            // Çalışan listesi (filtreleme için)
            ViewBag.Employees = _context.Employees
                .Where(e => e.IsActive)
                .Select(e => new { e.EmployeeId, FullName = e.FirstName + " " + e.LastName })
                .ToList();

            return View(leaveRequests);
        }

        [HttpPost]
        public IActionResult Approve(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var sql = "UPDATE LeaveRequests SET Status = 'Approved', ApprovedByUserId = {0} WHERE RequestId = {1}";
            _context.Database.ExecuteSqlRaw(sql, userId ?? (object)DBNull.Value, id);
            
            TempData["SuccessMessage"] = "İzin talebi onaylandı.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Reject(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var sql = "UPDATE LeaveRequests SET Status = 'Rejected', ApprovedByUserId = {0} WHERE RequestId = {1}";
            _context.Database.ExecuteSqlRaw(sql, userId ?? (object)DBNull.Value, id);

            TempData["SuccessMessage"] = "İzin talebi reddedildi.";
            return RedirectToAction("Index");
        }

        public IActionResult Details(int id)
        {
            var sql = @"
                SELECT 
                    lr.RequestId,
                    lr.EmployeeId,
                    e.FirstName AS EmployeeFirstName,
                    e.LastName AS EmployeeLastName,
                    d.DepartmentName,
                    j.JobTitle,
                    e.Email,
                    lt.TypeName AS LeaveTypeName,
                    CAST(lr.StartDate AS datetime) AS StartDate,
                    CAST(lr.EndDate AS datetime) AS EndDate,
                    lr.Status,
                    lr.Reason,
                    lr.ApprovedByUserId,
                    u.Username AS ApprovedByUserName
                FROM LeaveRequests lr
                INNER JOIN Employees e ON lr.EmployeeId = e.EmployeeId
                LEFT JOIN Departments d ON e.DepartmentId = d.DepartmentId
                LEFT JOIN Jobs j ON e.JobId = j.JobId
                INNER JOIN LeaveTypes lt ON lr.LeaveTypeId = lt.LeaveTypeId
                LEFT JOIN Users u ON lr.ApprovedByUserId = u.UserId
                WHERE lr.RequestId = {0}";

            var leave = _context.Database.SqlQueryRaw<LeaveRequestViewModel>(sql, id).AsEnumerable().FirstOrDefault();

            if (leave == null)
                return NotFound();

            return View(leave);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var employeeId = HttpContext.Session.GetInt32("EmployeeId");

            if (userId == null || employeeId == null)
            {
                TempData["ErrorMessage"] = "Oturum bilgisi bulunamadı. Lütfen tekrar giriş yapın.";
                return RedirectToAction("Index", "Login");
            }

            var employee = _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Job)
                .FirstOrDefault(e => e.EmployeeId == employeeId);

            if (employee == null)
            {
                TempData["ErrorMessage"] = "Çalışan bilgisi bulunamadı!";
                return RedirectToAction("Index", "Employee");
            }

            ViewBag.LeaveTypes = _context.LeaveTypes
                .Select(lt => new { lt.LeaveTypeId, lt.TypeName, lt.DaysAllowed })
                .ToList();

            ViewBag.Employee = employee;
            ViewBag.EmployeeId = employeeId;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(int leaveTypeId, DateTime startDate, DateTime endDate, string? reason)
        {
            try
            {
                var employeeId = HttpContext.Session.GetInt32("EmployeeId");

                if (employeeId == null)
                {
                    TempData["ErrorMessage"] = "Oturum bilgisi bulunamadı!";
                    return RedirectToAction("Index", "Login");
                }

                if (startDate >= endDate)
                {
                    TempData["ErrorMessage"] = "Bitiş tarihi, başlangıç tarihinden sonra olmalıdır!";
                    ReloadCreateDropdowns(employeeId.Value);
                    return View();
                }

                if (startDate < DateTime.Now.Date)
                {
                    TempData["ErrorMessage"] = "Geçmiş tarih için izin talebi oluşturamazsınız!";
                    ReloadCreateDropdowns(employeeId.Value);
                    return View();
                }

                var sql = @"
                    INSERT INTO LeaveRequests (EmployeeId, LeaveTypeId, StartDate, EndDate, Reason, Status)
                    VALUES ({0}, {1}, {2}, {3}, {4}, 'Pending')";

                _context.Database.ExecuteSqlRaw(sql, employeeId.Value, leaveTypeId, startDate, endDate, reason ?? (object)DBNull.Value);

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

