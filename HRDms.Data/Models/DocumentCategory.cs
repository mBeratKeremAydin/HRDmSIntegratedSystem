using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class DocumentCategory
{
    public int CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public int? ParentCategoryId { get; set; }
}
