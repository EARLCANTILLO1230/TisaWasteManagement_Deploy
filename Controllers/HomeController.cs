using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Data;
using TisaWasteManagement.Models;

namespace TisaWasteManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Load announcements (newest first) and Bulletin Board pictures
            // (newest first) so the public Home page can display them.
            ViewBag.Announcements = await _context.Announcement
                .OrderByDescending(a => a.DatePosted)
                .ToListAsync();

            ViewBag.BulletinBoardImages = await _context.BulletinBoardImage
                .OrderByDescending(b => b.DateUploaded)
                .ToListAsync();

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
        // Controllers/HomeController.cs - Add this method
        public IActionResult About()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
