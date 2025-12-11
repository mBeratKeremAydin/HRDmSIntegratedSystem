using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HRDms.Data.Models;

[MetadataType(typeof(LocationMetadata))]
public partial class Location
{
}

public class LocationMetadata
{
    [ValidateNever]
    public ICollection<Department> Departments { get; set; }
}