namespace HRDms.Data.Models 
{
    public class DocumentListViewModel
    {
        public int DocumentID { get; set; }
        public string Title { get; set; }
        public string CategoryName { get; set; }
        public string OwnerName { get; set; } 
        public DateTime CreatedDate { get; set; }
        public string CurrentStatus { get; set; }
    }
}