using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HR.Mvc.Models
{
    public class DepartmentViewModel
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string? LocationName { get; set; }
        public string? ManagerName { get; set; }
        public int EmployeeCount { get; set; }
    }

    public class DepartmentDetailViewModel
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string? LocationName { get; set; }
        public string? ManagerName { get; set; }
        [NotMapped]
        public List<DepartmentEmployeeDTO> Employees { get; set; } = new();
    }

    public class DepartmentEmployeeDTO
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; }
        public string? JobTitle { get; set; }
        public string Email { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime? HireDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class DepartmentCreateViewModel
    {
        public int DepartmentId { get; set; }

        [Required(ErrorMessage = "Department Name is required")]
        public string DepartmentName { get; set; }

        [Required(ErrorMessage = "Location is required")]
        public int LocationId { get; set; }

        public int? ManagerId { get; set; }

        public string? ManagerName { get; set; }
    }

    public class LocationDTO
    {
        public int LocationId { get; set; }
        public string LocationName { get; set; }
    }

    public class EmployeeSelectDTO
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; }
    }
}
