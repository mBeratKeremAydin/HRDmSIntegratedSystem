using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HRDms.Data.Models;

[MetadataType(typeof(EmployeeMetadata))]
public partial class Employee
{
}

public class EmployeeMetadata
{
    // DepartmentId gerekli ama Department navigation property'si değil
    [Required(ErrorMessage = "Departman seçilmelidir")]
    public int DepartmentId { get; set; }

    // Navigation property'leri validation'dan hariç tut
    [ValidateNever]
    public Department Department { get; set; }

    [ValidateNever]
    public ICollection<Document> Documents { get; set; }

    [ValidateNever]
    public ICollection<EmploymentContract> EmploymentContracts { get; set; }

    [ValidateNever]
    public Job? Job { get; set; }

    [ValidateNever]
    public Employee? Manager { get; set; }

    [ValidateNever]
    public User? User { get; set; }

    [ValidateNever]
    public ICollection<Attendance> Attendances { get; set; }

    [ValidateNever]
    public ICollection<Department> Departments { get; set; }

    [ValidateNever]
    public ICollection<Employee> InverseManager { get; set; }

    [ValidateNever]
    public ICollection<LeaveRequest> LeaveRequests { get; set; }

    [ValidateNever]
    public ICollection<PerformanceReview> PerformanceReviewEmployees { get; set; }

    [ValidateNever]
    public ICollection<PerformanceReview> PerformanceReviewReviewers { get; set; }
}