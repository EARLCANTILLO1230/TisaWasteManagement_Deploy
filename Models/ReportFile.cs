// Models/ReportFile.cs
//
// This is the DATABASE record for an uploaded file. It does NOT contain the
// file's actual bytes - only information ABOUT the file (name, category,
// who uploaded it) plus FilePath, which points to where the real file is
// sitting on disk (wwwroot/uploads/...). This keeps the database small and
// fast, while the actual documents live as normal files on the server.
using System.ComponentModel.DataAnnotations;

namespace TisaWasteManagement.Models
{
    public class ReportFile
    {
        [Key]
        public int ReportFileId { get; set; }

        [Required(ErrorMessage = "File name is required.")]
        [StringLength(200, ErrorMessage = "File name cannot exceed 200 characters.")]
        [Display(Name = "File Name")]
        public string FileName { get; set; } = string.Empty;

        // e.g. "PDF", "DOCX", "XLSX" - taken from the file's extension on upload
        [Display(Name = "File Type")]
        public string FileType { get; set; } = "Unknown";

        // Stored in kilobytes so the list page can show "45 KB" / "2.3 MB"
        [Display(Name = "File Size (KB)")]
        public long FileSize { get; set; }

        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Category")]
        public string Category { get; set; } = "General"; // General, Report, Guideline, or Resolution

        [Display(Name = "Uploaded By")]
        public string? UploadedBy { get; set; }

        [Display(Name = "Upload Date")]
        public DateTime UploadDate { get; set; } = DateTime.Now;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        // Web path to the actual file, e.g. "/uploads/3f6b1c2e_myfile.pdf"
        [Display(Name = "File Path")]
        public string? FilePath { get; set; }
    }
}
