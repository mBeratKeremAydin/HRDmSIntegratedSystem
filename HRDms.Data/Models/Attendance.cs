using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class Attendance
{
    public int AttendanceId { get; set; }

    public int EmployeeId { get; set; }

    public DateTime? CheckInTime { get; set; }

    public DateTime? CheckOutTime { get; set; }

    public DateOnly? Date { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
