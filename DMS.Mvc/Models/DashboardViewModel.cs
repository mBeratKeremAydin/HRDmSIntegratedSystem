using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Mvc.Models
{
    public class DashboardViewModel
    {
        // Kartlar için Sayaçlar
        public int TotalEmployeeCount { get; set; } // Toplam Personel
        public int DepartmentEmployeeCount { get; set; } // Sadece İlgili Departman
        public int MyDocumentCount { get; set; }    // Benim Yüklediklerim
        public int PendingApprovalCount { get; set; } // Onay Bekleyenler

        // Tablo için Liste
        public List<RecentDocumentViewModel> RecentDocuments { get; set; } = new List<RecentDocumentViewModel>();
    }

    // Tabloda göstereceğimiz ufak veri seti
    public class RecentDocumentViewModel
    {
        public int DocumentID { get; set; }
        public string Title { get; set; }
        public string OwnerName { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Status { get; set; }
    }
}