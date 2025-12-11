using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HRDms.Data.Models;

[MetadataType(typeof(DepartmentMetadata))]
public partial class Department
{
}

public class DepartmentMetadata
{
    [Required(ErrorMessage = "Departman adı zorunludur")]
    [StringLength(100, ErrorMessage = "Departman adı en fazla 100 karakter olabilir")]
    public string DepartmentName { get; set; }

    [Required(ErrorMessage = "Lokasyon seçilmelidir")]
    public int LocationId { get; set; }

    // Navigation property'leri validation'dan hariç tut
    [ValidateNever]
    public Location Location { get; set; }

    [ValidateNever]
    public Employee? Manager { get; set; }

    [ValidateNever]
    public ICollection<Employee> Employees { get; set; }

    [ValidateNever]
    public ICollection<DocumentPermission> DocumentPermissions { get; set; }
}