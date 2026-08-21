// Controllers/FileController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Data;
using TisaWasteManagement.Helpers;
using TisaWasteManagement.Models;

namespace TisaWasteManagement.Controllers
{
    // Only Admin can manage files (per the module spec)
    [RequireStaffRole("Admin")]
    public class FileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment; // used to find the wwwroot folder on disk

        // The only file types we allow to be uploaded (PDF, Word, Excel)
        private static readonly string[] AllowedContentTypes =
        {
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", // .xlsx
            "application/vnd.ms-excel",                                          // .xls
            "application/msword",                                                // .doc
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" // .docx
        };

        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public FileController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: File  -> list + search + filter
        public async Task<IActionResult> Index(string search, string categoryFilter)
        {
            var query = _context.ReportFiles.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                query = query.Where(f => f.FileName.Contains(search) ||
                                          (f.Description != null && f.Description.Contains(search)));
            }

            if (!string.IsNullOrEmpty(categoryFilter) && categoryFilter != "All")
            {
                query = query.Where(f => f.Category == categoryFilter);
            }

            var files = await query.OrderByDescending(f => f.UploadDate).ToListAsync();

            // Passed to the view so the search box / dropdown can remember what was searched for
            ViewBag.Search = search;
            ViewBag.CategoryFilter = categoryFilter;

            return View(files);
        }

        // GET: File/Upload -> shows the empty upload form
        public IActionResult Upload()
        {
            return View();
        }

        // POST: File/Upload -> handles the actual upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile? file, string description, string category)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("", "Please select a file to upload.");
                return View();
            }

            if (file.Length > MaxFileSizeBytes)
            {
                ModelState.AddModelError("", "File size must not exceed 10MB.");
                return View();
            }

            if (!AllowedContentTypes.Contains(file.ContentType))
            {
                ModelState.AddModelError("", "Only PDF, Excel, and Word files are allowed.");
                return View();
            }

            // Make sure wwwroot/uploads/ exists (create it the first time this runs)
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Prefix a GUID onto the original file name so two people uploading
            // "report.pdf" on the same day don't overwrite each other.
            var uniqueFileName = Guid.NewGuid() + "_" + file.FileName;
            var fullPathOnDisk = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(fullPathOnDisk, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var reportFile = new ReportFile
            {
                FileName = file.FileName, // the ORIGINAL name, shown to the user
                FilePath = "/uploads/" + uniqueFileName, // the actual saved location
                FileType = Path.GetExtension(file.FileName).TrimStart('.').ToUpper(),
                FileSize = file.Length / 1024, // convert bytes -> kilobytes
                Category = category,
                Description = description,
                UploadedBy = HttpContext.Session.GetString("StaffRole") ?? "Admin",
                UploadDate = DateTime.Now
            };

            _context.ReportFiles.Add(reportFile);
            await _context.SaveChangesAsync();

            TempData["Success"] = "File uploaded successfully!";
            return RedirectToAction(nameof(Index));
        }

        // GET: File/Download/5
        public async Task<IActionResult> Download(int? id)
        {
            if (id == null) return NotFound();

            var file = await _context.ReportFiles.FindAsync(id);
            if (file == null || string.IsNullOrEmpty(file.FilePath)) return NotFound();

            var fullPathOnDisk = Path.Combine(_webHostEnvironment.WebRootPath, file.FilePath.TrimStart('/'));

            if (!System.IO.File.Exists(fullPathOnDisk))
            {
                TempData["Error"] = "File not found on server.";
                return RedirectToAction(nameof(Index));
            }

            // Read the file into memory and send it to the browser as a download
            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPathOnDisk);
            return File(fileBytes, GetContentType(file.FileType), file.FileName);
        }

        // GET: File/Delete/5 -> confirmation page
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var file = await _context.ReportFiles.FindAsync(id);
            if (file == null) return NotFound();

            return View(file);
        }

        // POST: File/Delete/5 -> actually deletes the file + its database record
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var file = await _context.ReportFiles.FindAsync(id);
            if (file != null)
            {
                if (!string.IsNullOrEmpty(file.FilePath))
                {
                    var fullPathOnDisk = Path.Combine(_webHostEnvironment.WebRootPath, file.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(fullPathOnDisk))
                    {
                        System.IO.File.Delete(fullPathOnDisk);
                    }
                }

                _context.ReportFiles.Remove(file);
                await _context.SaveChangesAsync();
                TempData["Success"] = "File deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        // Maps a file extension (e.g. "PDF") to the MIME type the browser needs
        // in order to open/download it correctly.
        private string GetContentType(string fileType)
        {
            return fileType.ToLower() switch
            {
                "pdf" => "application/pdf",
                "doc" => "application/msword",
                "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "xls" => "application/vnd.ms-excel",
                "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
        }
    }
}
