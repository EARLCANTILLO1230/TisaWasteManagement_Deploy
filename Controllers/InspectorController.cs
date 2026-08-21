// Controllers/InspectorController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Data;
using TisaWasteManagement.Helpers;

namespace TisaWasteManagement.Controllers
{
    [RequireStaffRole("Inspector")]
    public class InspectorController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InspectorController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today.DayOfWeek;

            ViewBag.TodaysCollections = await _context.CollectionSchedule
                .CountAsync(s => s.DayOfWeek == today);

            ViewBag.PendingCollections = await _context.CollectionSchedule
                .CountAsync(s => s.Status == "Pending");

            ViewBag.CompletedCollections = await _context.CollectionSchedule
                .CountAsync(s => s.Status == "Completed");

            ViewBag.DelayedCollections = await _context.CollectionSchedule
                .CountAsync(s => s.Status == "Delayed");

            ViewBag.PendingComplaints = await _context.Complaint
                .CountAsync(c => c.Status == "Urgent" || c.Status == "Ongoing");

            return View();
        }
    }
}