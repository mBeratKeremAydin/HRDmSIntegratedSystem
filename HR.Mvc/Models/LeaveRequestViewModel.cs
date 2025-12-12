using System;

namespace HR.Mvc.Models
{
    public class LeaveRequestViewModel
    {
        public int RequestId { get; set; }
        public int EmployeeId { get; set; }
        public string? EmployeeFirstName { get; set; }
        public string? EmployeeLastName { get; set; }
        public string? DepartmentName { get; set; }
        public string? JobTitle { get; set; }
        public string? Email { get; set; }
        public string? LeaveTypeName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Status { get; set; }
        public string? Reason { get; set; }
        public int? ApprovedByUserId { get; set; }
        public string? ApprovedByUserName { get; set; }
        
        // Helper property for full name
        public string EmployeeFullName => $"{EmployeeFirstName} {EmployeeLastName}";
        
        // Helper for days calculation
        public int Days => (EndDate - StartDate).Days + 1;
    }
}
