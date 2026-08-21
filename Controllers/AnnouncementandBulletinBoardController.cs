// Controllers/AnnouncementandBulletinBoardController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Data;
using TisaWasteManagement.Helpers;
using TisaWasteManagement.Models;

namespace TisaWasteManagement.Controllers
{
    // Only Admin staff can manage announcements or bulletin board pictures.
    // (Everyone can still VIEW both - that happens on the public Home/Index
    // page via HomeController, which has no [RequireStaffRole].)
    //
    // This controller intentionally owns BOTH the Announcement and
    // BulletinBoardImage entities. They used to be two separate modules
    // (AnnouncementController + BulletinBoardController, each with its own
    // Views folder), but were merged into one "Announcements & Bulletin
    // Board" module so the admin only has one place to manage both kinds
    // of public posts. The two entities are still fully independent in the
    // database (separate tables, separate image folders) - only the
    // controller/views are combined.
    [RequireStaffRole("Admin")]
    public class AnnouncementandBulletinBoardController : Controller
    {
        private readonly ApplicationDbContext _context;

        // IWebHostEnvironment gives us the physical path to wwwroot so we
        // know where on disk to save uploaded pictures.
        private readonly IWebHostEnvironment _env;

        // Folder (inside wwwroot/images) where announcement pictures are stored.
        private const string AnnouncementImageFolder = "images/announcements";

        // Folder (inside wwwroot/images) where bulletin board pictures are stored.
        // Kept separate from the announcement folder so the two galleries'
        // files never collide, even though they're now managed from the
        // same controller.
        private const string BulletinBoardImageFolder = "images/bulletinboard";

        // Only these picture types are allowed to be uploaded.
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };

        // Simple size cap (5 MB) so an admin can't accidentally fill the disk
        // with one huge upload.
        private const long MaxFileSizeBytes = 5 * 1024 * 1024;

        public AnnouncementandBulletinBoardController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: /AnnouncementandBulletinBoard
        // Combined landing page for the module: lists every announcement
        // AND every bulletin board picture, newest first, for the admin to
        // manage from one screen.
        public async Task<IActionResult> Index()
        {
            var announcements = await _context.Announcement
                .OrderByDescending(a => a.DatePosted)
                .ToListAsync();

            var bulletinBoardImages = await _context.BulletinBoardImage
                .OrderByDescending(b => b.DateUploaded)
                .ToListAsync();

            ViewBag.BulletinBoardImages = bulletinBoardImages;

            return View(announcements);
        }

        // GET: /AnnouncementandBulletinBoard/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /AnnouncementandBulletinBoard/Create
        // "imageFile" is a separate parameter from "announcement" because a
        // file upload isn't a normal text field - ASP.NET Core model binding
        // fills it in from the <input type="file" name="imageFile"> element.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Announcement announcement, IFormFile? imageFile)
        {
            // ImageFileName is set by this action itself (see below), not typed
            // in by the admin, so it shouldn't be part of validation.
            ModelState.Remove(nameof(Announcement.ImageFileName));

            if (imageFile != null && imageFile.Length > 0 && !IsValidImage(imageFile, out string error))
            {
                ModelState.AddModelError(string.Empty, error);
            }

            if (!ModelState.IsValid)
            {
                return View(announcement);
            }

            if (imageFile != null && imageFile.Length > 0)
            {
                announcement.ImageFileName = await SaveImageAsync(imageFile, AnnouncementImageFolder);
            }

            announcement.DatePosted = DateTime.Now;

            _context.Announcement.Add(announcement);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Announcement posted successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /AnnouncementandBulletinBoard/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var announcement = await _context.Announcement.FindAsync(id);
            if (announcement == null) return NotFound();

            return View(announcement);
        }

        // POST: /AnnouncementandBulletinBoard/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Announcement announcement, IFormFile? imageFile)
        {
            if (id != announcement.AnnouncementId) return NotFound();

            ModelState.Remove(nameof(Announcement.ImageFileName));

            if (imageFile != null && imageFile.Length > 0 && !IsValidImage(imageFile, out string error))
            {
                ModelState.AddModelError(string.Empty, error);
            }

            if (!ModelState.IsValid)
            {
                return View(announcement);
            }

            // Load the current record (without tracking it) so we know the
            // existing picture and original post date, in case the admin
            // doesn't upload a new picture.
            var existing = await _context.Announcement
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AnnouncementId == id);
            if (existing == null) return NotFound();

            announcement.ImageFileName = existing.ImageFileName;
            announcement.DatePosted = existing.DatePosted;

            if (imageFile != null && imageFile.Length > 0)
            {
                // A new picture was uploaded - remove the old file from disk
                // first so we don't leave unused pictures behind.
                DeleteImageFile(existing.ImageFileName, AnnouncementImageFolder);
                announcement.ImageFileName = await SaveImageAsync(imageFile, AnnouncementImageFolder);
            }

            _context.Update(announcement);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Announcement updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /AnnouncementandBulletinBoard/Delete/5
        // Shows a confirmation page before actually deleting.
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var announcement = await _context.Announcement
                .FirstOrDefaultAsync(a => a.AnnouncementId == id);
            if (announcement == null) return NotFound();

            return View(announcement);
        }

        // POST: /AnnouncementandBulletinBoard/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var announcement = await _context.Announcement.FindAsync(id);
            if (announcement != null)
            {
                // Remove the picture file from wwwroot too, not just the database row.
                DeleteImageFile(announcement.ImageFileName, AnnouncementImageFolder);

                _context.Announcement.Remove(announcement);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Announcement deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        // --- Helpers ---------------------------------------------------

        // Checks the uploaded file's extension and size before we trust it.
        private bool IsValidImage(IFormFile file, out string error)
        {
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
            {
                error = "Only .jpg, .jpeg, .png, or .gif picture files are allowed.";
                return false;
            }

            if (file.Length > MaxFileSizeBytes)
            {
                error = "Picture file is too large. Maximum size is 5 MB.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        // Saves an uploaded picture into wwwroot/{folder} with a unique file
        // name, and returns just that file name (not the full path). Shared
        // by both the Announcement and Bulletin Board actions - only the
        // destination folder differs between the two.
        private async Task<string> SaveImageAsync(IFormFile imageFile, string folder)
        {
            string folderPath = Path.Combine(_env.WebRootPath, folder);

            // Create the folder the first time this runs, if it isn't there yet.
            Directory.CreateDirectory(folderPath);

            // A new Guid as the file name means two uploads can never collide,
            // even if two admins upload files that both happen to be named
            // "photo.jpg". We keep the original extension (.jpg, .png, etc.).
            string extension = Path.GetExtension(imageFile.FileName);
            string uniqueFileName = Guid.NewGuid().ToString() + extension;
            string fullPath = Path.Combine(folderPath, uniqueFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return uniqueFileName;
        }

        // Deletes a picture file from wwwroot/{folder}, if it exists.
        private void DeleteImageFile(string? fileName, string folder)
        {
            if (string.IsNullOrEmpty(fileName)) return;

            string fullPath = Path.Combine(_env.WebRootPath, folder, fileName);
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        // --- Bulletin Board actions -------------------------------------
        // Bulletin Board pictures are simpler than announcements (just a
        // picture + optional caption, no title/content/edit), but for
        // consistency with the rest of this module they get their own
        // dedicated Create and Delete pages instead of the old inline
        // upload form.

        // GET: /AnnouncementandBulletinBoard/BulletinBoardCreate
        public IActionResult BulletinBoardCreate()
        {
            return View();
        }

        // POST: /AnnouncementandBulletinBoard/BulletinBoardCreate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulletinBoardCreate(IFormFile imageFile, string? caption)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Please choose a picture to upload.");
            }
            else if (!IsValidImage(imageFile, out string error))
            {
                ModelState.AddModelError(string.Empty, error);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Caption = caption;
                return View();
            }

            var bulletinImage = new BulletinBoardImage
            {
                ImageFileName = await SaveImageAsync(imageFile!, BulletinBoardImageFolder),
                Caption = caption,
                DateUploaded = DateTime.Now
            };

            _context.BulletinBoardImage.Add(bulletinImage);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Picture added to the Bulletin Board.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /AnnouncementandBulletinBoard/BulletinBoardDelete/5
        // Shows a confirmation page before actually deleting, same pattern
        // as the Announcement Delete page.
        public async Task<IActionResult> BulletinBoardDelete(int? id)
        {
            if (id == null) return NotFound();

            var image = await _context.BulletinBoardImage
                .FirstOrDefaultAsync(b => b.BulletinBoardImageId == id);
            if (image == null) return NotFound();

            return View(image);
        }

        // POST: /AnnouncementandBulletinBoard/BulletinBoardDelete/5
        [HttpPost, ActionName("BulletinBoardDelete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulletinBoardDeleteConfirmed(int id)
        {
            var image = await _context.BulletinBoardImage.FindAsync(id);
            if (image != null)
            {
                // Remove the picture file from wwwroot too, not just the database row.
                DeleteImageFile(image.ImageFileName, BulletinBoardImageFolder);

                _context.BulletinBoardImage.Remove(image);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Picture removed from the Bulletin Board.";
            return RedirectToAction(nameof(Index));
        }
    }
}
