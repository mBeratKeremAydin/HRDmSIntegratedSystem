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

    public virtual Employee? Employee { get; set; }

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
