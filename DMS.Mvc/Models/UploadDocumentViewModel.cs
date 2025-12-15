using System.ComponentModel.DataAnnotations;

namespace HRDms.Data.Models
{
    public class UploadDocumentViewModel
    {
        [Required(ErrorMessage = "Başlık zorunludur.")]
        public string Title { get; set; }

        public string Description { get; set; }

        public List<int> SelectedDepartmentIDs { get; set; } = new List<int>();

        public List<Department> Departments { get; set; } = new List<Department>();

        [Required(ErrorMessage = "Kategori seçmelisiniz.")]
        public int CategoryID { get; set; }

        [Required(ErrorMessage = "Lütfen bir dosya seçin.")]
        public IFormFile File { get; set; } 

        public List<DocumentCategory>? Categories { get; set; }
    }
}