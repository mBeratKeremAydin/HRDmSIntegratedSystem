using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace HR.Mvc.Models
{
    public class EmployeeAttendanceViewModel
    {
        public int EmployeeId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string JobTitle { get; set; }
        public string DepartmentName { get; set; }
        
        public List<AttendanceViewModel> Attendances { get; set; } = new();
    }

    public class AttendanceViewModel
    {
        public int AttendanceId { get; set; }
        public DateTime? Date { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
    }
}
