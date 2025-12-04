using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class Employee
{
    public int EmployeeId { get; set; }

    public int? UserId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? IdentityNumber { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public DateOnly? HireDate { get; set; }

    public int DepartmentId { get; set; }

    public int? JobId { get; set; }

    public int? ManagerId { get; set; }

    public bool IsActive { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    public virtual ICollection<Employee> InverseManager { get; set; } = new List<Employee>();

    public virtual Job? Job { get; set; }

    public virtual Employee? Manager { get; set; }

    public virtual User? User { get; set; }
}
