using System;
using System.Collections.Generic;

namespace HR.Mvc.Models
{
    public class EmployeeBasicInfoDTO
    {
        public int EmployeeId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string JobTitle { get; set; }
        public string DepartmentName { get; set; }
    }

    public class EmployeeDashboardViewModel : EmployeeBasicInfoDTO
    {
        public int TotalLeaveRequests { get; set; }
        public int PendingLeaveRequests { get; set; }
        public int ApprovedLeaveRequests { get; set; }
        public int TotalAttendanceDays { get; set; }
        public int TotalDocuments { get; set; }
        public int PerformanceReviews { get; set; }
        public int CurrentMonthAttendance { get; set; }
        
        public List<DashboardLeaveRequestViewModel> RecentLeaveRequests { get; set; } = new();
    }

    public class DashboardLeaveRequestViewModel 
    {
        public string TypeName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
    }
}
