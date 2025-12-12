using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace HR.Mvc.Models
{
    public class EmployeeDetailsDto
    {
        public int EmployeeId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? IdentityNumber { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? HireDate { get; set; }
        
        public string? JobTitle { get; set; }
        public string? DepartmentName { get; set; }
        
        // Manager Info
        public string? ManagerFirstName { get; set; }
        public string? ManagerLastName { get; set; }
        public string? ManagerEmail { get; set; }
        public string? ManagerJobTitle { get; set; }
    }

    public class EmployeeDetailsViewModel : EmployeeDetailsDto
    {
        public EmploymentContractViewModel ActiveContract { get; set; }
    }

    public class EmploymentContractViewModel
    {
        public string ContractType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? Salary { get; set; }
    }
}
