using System.ComponentModel.DataAnnotations;

namespace HRDms.Data.Models
{
    public class UploadDocumentViewModel
    {
        [Required(ErrorMessage = "Başlık zorunludur.")]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required(ErrorMessage = "Kategori seçmelisiniz.")]
        public int CategoryID { get; set; }

        [Required(ErrorMessage = "Lütfen bir dosya seçin.")]
        public IFormFile File { get; set; } // Dosyayı HTML formundan buraya alacağız

        // Dropdown listesini doldurmak için kullanacağız
        public List<DocumentCategory>? Categories { get; set; }
    }
}