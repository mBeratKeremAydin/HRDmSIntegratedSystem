using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class Job
{
    public int JobId { get; set; }

    public string JobTitle { get; set; } = null!;

    public decimal? MinSalary { get; set; }

    public decimal? MaxSalary { get; set; }

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
