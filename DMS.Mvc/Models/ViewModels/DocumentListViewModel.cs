namespace HRDms.Data.Models // Namespace'i projene göre ayarla
{
    public class DocumentListViewModel
    {
        public int DocumentID { get; set; }
        public string Title { get; set; }
        public string CategoryName { get; set; }
        public string OwnerName { get; set; } // Ad + Soyad birleşik gelecek
        public DateTime CreatedDate { get; set; }
        public string CurrentStatus { get; set; }
    }
}