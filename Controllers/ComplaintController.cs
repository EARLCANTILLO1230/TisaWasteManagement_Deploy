using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Data;
using TisaWasteManagement.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;

namespace TisaWasteManagement.Controllers
{
    /// <summary>
    /// Handles filing and tracking of waste complaints from residents.
    /// No login required - residents can submit and track complaints anonymously.
    /// </summary>
    public class ComplaintController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        // IWebHostEnvironment gives us the physical path to wwwroot so we
        // know where on disk to save uploaded complaint photos.
        // Same pattern as AnnouncementController / BulletinBoardController.
        private readonly IWebHostEnvironment _env;

        // Folder (inside wwwroot/images) where complaint photos are stored.
        private const string ImageFolder = "images/complaints";

        // Only these picture types are allowed to be uploaded.
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        // Simple size cap (5 MB), same limit as before.
        private const long MaxFileSizeBytes = 5 * 1024 * 1024;

        public ComplaintController(ApplicationDbContext context, IConfiguration configuration, IWebHostEnvironment env)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = new HttpClient();
            _env = env;
        }

        /// <summary>
        /// GET: Complaint/Index
        /// Displays the complaint submission form.
        /// </summary>
        public IActionResult Index()
        {
            // Load the list of active sitios for the dropdown
            LoadSitioDropdown();

            // ✅ Pass the reCAPTCHA site key to the view
            ViewBag.RecaptchaSiteKey = _configuration["Recaptcha:SiteKey"];

            return View();
        }

        /// <summary>
        /// POST: Complaint/Index
        /// Handles complaint submission, generates ticket number, and saves to database.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)] // Allow a little headroom over the 5MB image limit for the rest of the form
        public async Task<IActionResult> Index([Bind("ResidentName,ContactNumber,SitioId,ComplaintType,Details")] Complaint complaint, IFormFile? ImageFile)
        {
            // Reload dropdown in case we need to redisplay the form with errors
            LoadSitioDropdown();

            // ✅ Pass the reCAPTCHA site key to the view (needed when returning the view)
            ViewBag.RecaptchaSiteKey = _configuration["Recaptcha:SiteKey"];

            // ✅ Verify CAPTCHA - Check before ModelState validation
            var recaptchaResponse = Request.Form["g-recaptcha-response"];
            if (!await VerifyRecaptcha(recaptchaResponse))
            {
                ModelState.AddModelError(string.Empty, "Please complete the CAPTCHA verification.");
                TempData["Error"] = "Please complete the CAPTCHA verification.";
                return View(complaint);
            }

            // Check if all required fields are filled
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please complete all required fields.";
                return View(complaint);
            }

            // Verify that the selected Sitio exists and is active
            var sitio = await _context.Sitio.FindAsync(complaint.SitioId);
            if (sitio == null)
            {
                ModelState.AddModelError("SitioId", "Selected Sitio is not available.");
                return View(complaint);
            }

            // Full Name is optional - default to "Anonymous" when left blank
            if (string.IsNullOrWhiteSpace(complaint.ResidentName))
            {
                complaint.ResidentName = "Anonymous";
            }

            // A photo is now required - the resident must attach one before submitting
            if (ImageFile == null || ImageFile.Length == 0)
            {
                ModelState.AddModelError("ImageFile", "Please attach a photo.");
                TempData["Error"] = "Please attach a photo of the issue.";
                return View(complaint);
            }

            // Handle the (now required) image upload.
            // Instead of storing the picture's bytes in the database, we save the
            // picture as a file inside wwwroot/images/complaints and only store
            // the generated file name on the Complaint record - same approach as
            // the Announcement and BulletinBoardImage modules.
            if (ImageFile != null && ImageFile.Length > 0)
            {
                string extension = Path.GetExtension(ImageFile.FileName).ToLowerInvariant();

                if (!AllowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("ImageFile", "Only JPG, PNG, GIF, or WEBP images are allowed.");
                    TempData["Error"] = "Unsupported image file type.";
                    return View(complaint);
                }

                if (ImageFile.Length > MaxFileSizeBytes)
                {
                    ModelState.AddModelError("ImageFile", "Image size must not exceed 5MB.");
                    TempData["Error"] = "The uploaded image exceeds the 5MB size limit.";
                    return View(complaint);
                }

                complaint.ImageFileName = await SaveImageAsync(ImageFile, extension);
            }

            // Generate a unique ticket number
            // Format: TISA-YYYYMMDD-XXXX (e.g., TISA-20260718-0001)
            complaint.TicketNumber = await GenerateTicketNumberAsync();

            // Set default status to "Awaiting Review" for new complaints
            complaint.Status = "Awaiting Review";
            complaint.FiledDate = DateTime.Now;

            // Save the complaint to the database
            _context.Complaint.Add(complaint);
            await _context.SaveChangesAsync();

            // Store the ticket number in TempData to show on confirmation page
            TempData["TicketNumber"] = complaint.TicketNumber;
            TempData["ComplaintSuccess"] = "Your complaint has been filed successfully!";

            // Redirect to the confirmation page
            return RedirectToAction("Confirmation", new { ticketNumber = complaint.TicketNumber });
        }

        /// <summary>
        /// GET: Complaint/Track
        /// Displays the ticket tracking form.
        /// </summary>
        public IActionResult Track()
        {
            return View();
        }

        /// <summary>
        /// POST: Complaint/Track
        /// Finds and displays complaint details based on ticket number.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Track(string ticketNumber)
        {
            if (string.IsNullOrEmpty(ticketNumber))
            {
                TempData["Error"] = "Please enter a ticket number.";
                return View();
            }

            // Search for the complaint with the given ticket number
            var complaint = await _context.Complaint
                .Include(c => c.Sitio)  // Include Sitio details
                .FirstOrDefaultAsync(c => c.TicketNumber == ticketNumber);

            if (complaint == null)
            {
                TempData["Error"] = "Ticket number not found. Please check and try again.";
                return View();
            }

            // Return the complaint to display its status
            return View("TrackResult", complaint);
        }

        /// <summary>
        /// GET: Complaint/Confirmation
        /// Shows confirmation message and ticket number after successful submission.
        /// </summary>
        public IActionResult Confirmation(string ticketNumber)
        {
            if (string.IsNullOrEmpty(ticketNumber))
            {
                return RedirectToAction("Index");
            }

            // Get the complaint to display full details on confirmation page
            var complaint = _context.Complaint
                .Include(c => c.Sitio)
                .FirstOrDefault(c => c.TicketNumber == ticketNumber);

            if (complaint == null)
            {
                TempData["Error"] = "Complaint not found.";
                return RedirectToAction("Index");
            }

            return View(complaint);
        }

        /// <summary>
        /// Verifies the reCAPTCHA response with Google's API.
        /// </summary>
        private async Task<bool> VerifyRecaptcha(string recaptchaResponse)
        {
            if (string.IsNullOrEmpty(recaptchaResponse))
                return false;

            string secretKey = _configuration["Recaptcha:SecretKey"];
            if (string.IsNullOrEmpty(secretKey))
                return false;

            try
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("secret", secretKey),
                    new KeyValuePair<string, string>("response", recaptchaResponse)
                });

                var response = await _httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);

                if (!response.IsSuccessStatusCode)
                    return false;

                string jsonResponse = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(jsonResponse);
                JsonElement root = document.RootElement;

                // Check if the verification was successful
                if (root.TryGetProperty("success", out JsonElement successElement))
                {
                    bool success = successElement.GetBoolean();

                    // Optional: Check the score (for reCAPTCHA v3)
                    if (root.TryGetProperty("score", out JsonElement scoreElement))
                    {
                        float score = scoreElement.GetSingle();
                        // You can set a threshold, e.g., score >= 0.5
                        return success && score >= 0.5;
                    }

                    return success;
                }

                return false;
            }
            catch
            {
                // Log the exception if you have logging set up
                return false;
            }
        }

        /// <summary>
        /// Generates a unique ticket number.
        /// Format: TISA-YYYYMMDD-XXXX (incremental per day)
        /// </summary>
        private async Task<string> GenerateTicketNumberAsync()
        {
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string prefix = "TISA";

            // Get the count of complaints filed today to determine the sequence number
            DateTime today = DateTime.Today;
            int todayCount = await _context.Complaint
                .CountAsync(c => c.FiledDate >= today && c.FiledDate < today.AddDays(1));

            // Increment by 1 and format as 4-digit with leading zeros
            int sequenceNumber = todayCount + 1;
            string sequencePart = sequenceNumber.ToString("D4"); // D4 = 4 digits with leading zeros

            return $"{prefix}-{datePart}-{sequencePart}";
        }

        /// <summary>
        /// Saves an uploaded complaint photo into wwwroot/images/complaints with a
        /// unique file name, and returns just that file name (not the full path).
        /// This is the same pattern used by AnnouncementController and
        /// BulletinBoardController for saving their pictures.
        /// </summary>
        private async Task<string> SaveImageAsync(IFormFile imageFile, string extension)
        {
            // Build the full folder path: wwwroot/images/complaints
            string folderPath = Path.Combine(_env.WebRootPath, ImageFolder);

            // Create the folder the first time this runs, if it isn't there yet.
            Directory.CreateDirectory(folderPath);

            // A new Guid as the file name means two uploads can never collide,
            // even if two residents upload files that both happen to be named
            // "photo.jpg". We keep the original extension (.jpg, .png, etc.).
            string uniqueFileName = Guid.NewGuid().ToString() + extension;
            string fullPath = Path.Combine(folderPath, uniqueFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return uniqueFileName;
        }

        /// <summary>
        /// Loads the list of active sitios for dropdowns.
        /// </summary>
        private void LoadSitioDropdown()
        {
            ViewBag.SitioId = new SelectList(
                _context.Sitio
                    .OrderBy(s => s.SitioName),
                "SitioId",
                "SitioName"
            );

            // Complaint Types dropdown
            ViewBag.ComplaintTypes = new SelectList(
                new[]
                {
                    new { Value = "Missed Collection", Text = "Missed Collection" },
                    new { Value = "Illegal Dumping", Text = "Illegal Dumping" },
                    new { Value = "Other", Text = "Other" }
                },
                "Value",
                "Text"
            );
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
                _httpClient.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}