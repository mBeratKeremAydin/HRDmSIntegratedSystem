using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class LeaveType
{
    public int LeaveTypeId { get; set; }

    public string? TypeName { get; set; }

    public int? DaysAllowed { get; set; }

    public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
}
