using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class Location
{
    public int LocationId { get; set; }

    public string LocationName { get; set; } = null!;

    public string? Address { get; set; }

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();
}
