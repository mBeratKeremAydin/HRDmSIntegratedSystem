using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class Document
{
    public int DocumentId { get; set; }

    public string? Title { get; set; }

    public string? DocumentDescription { get; set; }

    public int CategoryId { get; set; }

    public int OwnerEmployeeId { get; set; }

    public DateTime CreatedDate { get; set; }

    public string CurrentStatus { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual DocumentCategory Category { get; set; } = null!;

    public virtual ICollection<DocumentPermission> DocumentPermissions { get; set; } = new List<DocumentPermission>();

    public virtual ICollection<DocumentStatusHistory> DocumentStatusHistories { get; set; } = new List<DocumentStatusHistory>();

    public virtual ICollection<DocumentVersion> DocumentVersions { get; set; } = new List<DocumentVersion>();

    public virtual Employee OwnerEmployee { get; set; } = null!;
}
