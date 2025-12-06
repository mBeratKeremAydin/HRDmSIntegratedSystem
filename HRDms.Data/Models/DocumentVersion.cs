using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class DocumentVersion
{
    public int VersionId { get; set; }

    public int DocumentId { get; set; }

    public int VersionNumber { get; set; }

    public string? FilePath { get; set; }

    public string? FileExtension { get; set; }

    public int? UploadedByUserId { get; set; }

    public DateTime? UploadDate { get; set; }

    public string? ChangeNote { get; set; }

    public virtual Document Document { get; set; } = null!;

    public virtual User? UploadedByUser { get; set; }
}
