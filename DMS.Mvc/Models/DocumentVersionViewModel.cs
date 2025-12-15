namespace HRDms.Data.Models
{
    public class DocumentVersionViewModel
    {
        public int VersionID { get; set; }
        public int VersionNumber { get; set; }
        public string FileName { get; set; } 
        public DateTime? UploadDate { get; set; }
        public string UploadedByName { get; set; }
        public string ChangeNote { get; set; }
    }
}