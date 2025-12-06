using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class DocumentCategory
{
    public int CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public int? ParentCategoryId { get; set; }

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<DocumentCategory> InverseParentCategory { get; set; } = new List<DocumentCategory>();

    public virtual DocumentCategory? ParentCategory { get; set; }
}
