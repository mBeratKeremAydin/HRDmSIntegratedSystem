using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class Department
{
    public int DepartmentId { get; set; }

    public string DepartmentName { get; set; } = null!;

    public int LocationId { get; set; }

    public int? ManagerId { get; set; }

    public virtual ICollection<DocumentPermission> DocumentPermissions { get; set; } = new List<DocumentPermission>();

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual Location Location { get; set; } = null!;

    public virtual Employee? Manager { get; set; }
}
