using System;
using System.Collections.Generic;

namespace HRDms.Data.Models;

public partial class DocumentStatusHistory
{
    public int HistoryId { get; set; }

    public int DocumentId { get; set; }

    public string? Status { get; set; }

    public int? ChangedByUserId { get; set; }

    public DateTime? ChangeDate { get; set; }

    public virtual User? ChangedByUser { get; set; }

    public virtual Document Document { get; set; } = null!;
}
