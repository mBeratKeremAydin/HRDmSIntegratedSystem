using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class EmploymentContract
{
    public int ContractId { get; set; }

    public int EmployeeId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public decimal Salary { get; set; }

    public string? ContractType { get; set; }

    public bool IsActive { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
