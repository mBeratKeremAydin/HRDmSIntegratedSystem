using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class PerformanceReview
{
    public int ReviewId { get; set; }

    public int? EmployeeId { get; set; }

    public int? ReviewerId { get; set; }

    public DateOnly? ReviewDate { get; set; }

    public int Score { get; set; }

    public string? Notes { get; set; }

    public virtual Employee? Employee { get; set; }

    public virtual Employee? Reviewer { get; set; }
}
