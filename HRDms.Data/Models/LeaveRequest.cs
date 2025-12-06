using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class LeaveRequest
{
    public int RequestId { get; set; }

    public int EmployeeId { get; set; }

    public int LeaveTypeId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = null!;

    public int? ApprovedByUserId { get; set; }

    public virtual User? ApprovedByUser { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual LeaveType LeaveType { get; set; } = null!;
}
