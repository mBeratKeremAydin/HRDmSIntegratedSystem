using System.ComponentModel.DataAnnotations.Schema; 

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

        [NotMapped] 
        public string FileName { get; set; }

        [NotMapped] 
        public List<StatusHistoryViewModel> History { get; set; } = new List<StatusHistoryViewModel>();

        [NotMapped]
        public List<DocumentVersionViewModel> Versions { get; set; } = new List<DocumentVersionViewModel>();

    }
}