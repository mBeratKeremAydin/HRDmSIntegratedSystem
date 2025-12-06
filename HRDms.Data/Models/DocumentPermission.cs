using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class DocumentPermission
{
    public int PermissionId { get; set; }

    public int DocumentId { get; set; }

    public int DepartmentId { get; set; }

    public bool? CanRead { get; set; }

    public bool? CanEdit { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual Document Document { get; set; } = null!;
}
