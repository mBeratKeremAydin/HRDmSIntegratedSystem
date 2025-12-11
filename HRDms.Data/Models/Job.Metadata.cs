using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HRDms.Data.Models;

[MetadataType(typeof(JobMetadata))]
public partial class Job
{
}

public class JobMetadata
{
    [ValidateNever]
    public ICollection<Employee> Employees { get; set; }
}