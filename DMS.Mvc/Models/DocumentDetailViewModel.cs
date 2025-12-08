using System.ComponentModel.DataAnnotations.Schema; // Bu kütüphanenin ekli olduğundan emin ol

namespace HRDms.Data.Models
{
    public class DocumentDetailViewModel
    {
        public int DocumentID { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string CategoryName { get; set; }
        public string OwnerName { get; set; }
        public string CurrentStatus { get; set; }
        public DateTime CreatedDate { get; set; }

        public string FilePath { get; set; }

        [NotMapped] // Dosya adını kodla dolduruyoruz, SQL'den gelmiyor.
        public string FileName { get; set; }

        // --- DÜZELTİLEN KISIM BURASI ---
        [NotMapped] // EF Core burayı SQL'den doldurmaya çalışmasın. Biz aşağıda elle dolduracağız.
        public List<StatusHistoryViewModel> History { get; set; } = new List<StatusHistoryViewModel>();

        [NotMapped]
        public List<DocumentVersionViewModel> Versions { get; set; } = new List<DocumentVersionViewModel>();

    }
}