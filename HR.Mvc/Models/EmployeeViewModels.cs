using System;
using System.Collections.Generic;

namespace HR.Mvc.Models
{
    public class EmployeeLeavesViewModel : EmployeeBasicInfoDTO
    {
        public List<LeaveRequestViewModel> LeaveRequests { get; set; } = new();
    }

    public class EmployeePerformanceViewModel : EmployeeBasicInfoDTO
    {
        public List<PerformanceReviewViewModel> Reviews { get; set; } = new();
    }

    public class PerformanceReviewViewModel
    {
        public int ReviewId { get; set; }
        public int EmployeeId { get; set; }
        public DateOnly? ReviewDate { get; set; }
        public int? Score { get; set; }
        public string? Notes { get; set; }
        public string? ReviewerName { get; set; }
    }

    public class PerformanceReviewCreateViewModel
    {
        public int EmployeeId { get; set; }
        public DateOnly ReviewDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public int Score { get; set; }
        public string? Notes { get; set; }
    }

    public class EmployeeDocumentsViewModel : EmployeeBasicInfoDTO
    {
        public List<DocumentViewModel> Documents { get; set; } = new();
    }

    public class DocumentViewModel
    {
        public int DocumentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Status { get; set; }
        public string CategoryName { get; set; }
        public bool IsActive { get; set; }
    }

    public class DepartmentDTO
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
    }

    public class JobDTO
    {
        public int JobId { get; set; }
        public string JobTitle { get; set; }
    }

    public class UserDTO
    {
        public int UserId { get; set; }
        public string Username { get; set; }
    }

    public class EmployeeIdUserIdDTO
    {
        public int EmployeeId { get; set; }
        public int? UserId { get; set; }
    }
}
