using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HRDms.Data.Models;

[MetadataType(typeof(RoleMetadata))]
public partial class Role
{
}

public class RoleMetadata
{
    [ValidateNever]
    public ICollection<UserRole> UserRoles { get; set; }
}