using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HRDms.Data.Models;

[MetadataType(typeof(LeaveTypeMetadata))]
public partial class LeaveType
{
}

public class LeaveTypeMetadata
{
    [ValidateNever]
    public ICollection<LeaveRequest> LeaveRequests { get; set; }
}