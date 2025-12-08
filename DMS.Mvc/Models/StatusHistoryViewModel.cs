namespace HRDms.Data.Models
{
    public class StatusHistoryViewModel
    {
        public string Status { get; set; }
        public string ChangedByName { get; set; } // Değiştiren kullanıcının adı
        public DateTime ChangeDate { get; set; }
    }
}