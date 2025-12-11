using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HRDms.Data.Models;

[MetadataType(typeof(PerformanceReviewMetadata))]
public partial class PerformanceReview
{
}

public class PerformanceReviewMetadata
{
    [ValidateNever]
    public Employee? Employee { get; set; }

    [ValidateNever]
    public Employee? Reviewer { get; set; }
}