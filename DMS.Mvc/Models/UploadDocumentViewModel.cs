using System.ComponentModel.DataAnnotations;

namespace HRDms.Data.Models
{
    public class UploadDocumentViewModel
    {
        [Required(ErrorMessage = "Başlık zorunludur.")]
        public string Title { get; set; }

        public string Description { get; set; }

        // YENİ: Kullanıcının seçtiği departman ID'leri (Çoklu Seçim)
        public List<int> SelectedDepartmentIDs { get; set; } = new List<int>();

        // YENİ: Formda göstermek için tüm departman listesi
        public List<Department> Departments { get; set; } = new List<Department>();

        [Required(ErrorMessage = "Kategori seçmelisiniz.")]
        public int CategoryID { get; set; }

        [Required(ErrorMessage = "Lütfen bir dosya seçin.")]
        public IFormFile File { get; set; } // Dosyayı HTML formundan buraya alacağız

        // Dropdown listesini doldurmak için kullanacağız
        public List<DocumentCategory>? Categories { get; set; }
    }
}