using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public string UserPassword { get; set; } = null!;

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<DocumentStatusHistory> DocumentStatusHistories { get; set; } = new List<DocumentStatusHistory>();

    public virtual ICollection<DocumentVersion> DocumentVersions { get; set; } = new List<DocumentVersion>();

    public virtual Employee? Employee { get; set; }

    public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
