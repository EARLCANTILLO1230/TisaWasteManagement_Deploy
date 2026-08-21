// Controllers/AdminController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Data;
using TisaWasteManagement.Helpers;

namespace TisaWasteManagement.Controllers
{
    [RequireStaffRole("Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalSitios = await _context.Sitio.CountAsync();
            ViewBag.TotalCollectors = await _context.Collector.CountAsync();
            ViewBag.TotalTrucks = await _context.GarbageTruck.CountAsync();
            ViewBag.TotalSchedules = await _context.CollectionSchedule.CountAsync();
            ViewBag.TotalComplaints = await _context.Complaint.CountAsync();
            ViewBag.PendingComplaints = await _context.Complaint.CountAsync(c => c.Status == "Urgent" || c.Status == "Ongoing");
            ViewBag.CompletedCollections = await _context.CollectionSchedule.CountAsync(s => s.Status == "Completed");

            return View();
        }
    }
}