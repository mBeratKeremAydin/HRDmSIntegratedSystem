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
}
