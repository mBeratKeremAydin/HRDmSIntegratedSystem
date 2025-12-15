using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Mvc.Models
{
    public class DashboardViewModel
    {
        public int TotalEmployeeCount { get; set; } 
        public int DepartmentEmployeeCount { get; set; } 
        public int MyDocumentCount { get; set; }    
        public int PendingApprovalCount { get; set; } 

        public List<RecentDocumentViewModel> RecentDocuments { get; set; } = new List<RecentDocumentViewModel>();
    }

    public class RecentDocumentViewModel
    {
        public int DocumentID { get; set; }
        public string Title { get; set; }
        public string OwnerName { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Status { get; set; }
    }
}