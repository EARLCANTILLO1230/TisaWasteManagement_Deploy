using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Data;
using TisaWasteManagement.Models;
using TisaWasteManagement.Helpers;
using System.Linq;
using System.Threading.Tasks;

namespace TisaWasteManagement.Controllers
{
    /// <summary>
    /// Records Management module - READ ONLY archive of system records.
    /// Displays Collection Schedule & Monitoring records and Complaint records.
    /// No Create, Edit, or Delete operations.
    /// 
    /// IMPORTANT: Only shows historical/finalized records:
    /// - Collection Records: Only Completed or Delayed schedules
    /// - Complaint Records: All complaints (they become records once submitted)
    /// </summary>
    [RequireStaffRole("Admin")]
    public class RecordsManagementController : Controller
    {
        // Database context used to access application data.
        private readonly ApplicationDbContext _context;

        // Constructor that injects the application's database context.
        public RecordsManagementController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Records/Index
        // Displays the Records Management dashboard with two tabs:
        // Collection Records (Completed/Delayed only) and Complaint Records.
        public async Task<IActionResult> Index(
            string collectionSearch = null,
            string collectionStatus = null,
            DateTime? collectionStartDate = null,
            DateTime? collectionEndDate = null,
            string complaintSearch = null,
            string complaintStatus = null,
            DateTime? complaintStartDate = null,
            DateTime? complaintEndDate = null)
        {
            // ---------- COLLECTION RECORDS ----------
            // IMPORTANT: Only show Completed and Delayed schedules.
            // Pending schedules remain in Collection Schedule (active operations).
            var collectionQuery = _context.CollectionSchedule
                .Include(s => s.GarbageTruck)
                .Include(s => s.Driver)
                .Include(s => s.CollectionScheduleCollectors).ThenInclude(csc => csc.Collector)
                .Include(s => s.CollectionScheduleSitios).ThenInclude(css => css.Sitio)
                .Where(s => s.Status == "Completed" || s.Status == "Delayed") // ← KEY CHANGE: Only historical records
                .AsQueryable();

            // Apply search filter for collection records
            if (!string.IsNullOrEmpty(collectionSearch))
            {
                collectionSearch = collectionSearch.Trim();
                collectionQuery = collectionQuery.Where(s =>
                    (s.Driver != null && (
                        EF.Functions.Like(s.Driver.FirstName, $"%{collectionSearch}%") ||
                        EF.Functions.Like(s.Driver.LastName, $"%{collectionSearch}%") ||
                        EF.Functions.Like(s.Driver.FirstName + " " + s.Driver.LastName, $"%{collectionSearch}%"))) ||
                    (s.GarbageTruck != null && EF.Functions.Like(s.GarbageTruck.PlateNumber, $"%{collectionSearch}%")) ||
                    (s.GarbageTruck != null && EF.Functions.Like(s.GarbageTruck.MVFileNumber, $"%{collectionSearch}%")) ||
                    (s.CollectionScheduleSitios.Any(css => EF.Functions.Like(css.Sitio.SitioName, $"%{collectionSearch}%"))) ||
                    (s.CollectionScheduleCollectors.Any(csc =>
                        EF.Functions.Like(csc.Collector.FirstName, $"%{collectionSearch}%") ||
                        EF.Functions.Like(csc.Collector.LastName, $"%{collectionSearch}%") ||
                        EF.Functions.Like(csc.Collector.FirstName + " " + csc.Collector.LastName, $"%{collectionSearch}%")))
                );
            }

            // Apply status filter for collection records (only Completed or Delayed)
            if (!string.IsNullOrEmpty(collectionStatus))
            {
                // Only allow filtering by Completed or Delayed since those are the only ones shown
                if (collectionStatus == "Completed" || collectionStatus == "Delayed")
                {
                    collectionQuery = collectionQuery.Where(s => s.Status == collectionStatus);
                }
                // If "Pending" is selected, return empty since Pending isn't in Records Management
                else if (collectionStatus == "Pending")
                {
                    collectionQuery = collectionQuery.Where(s => false);
                }
            }

            // Apply date range filter for collection records
            if (collectionStartDate.HasValue)
            {
                collectionQuery = collectionQuery.Where(s => s.CreatedDate >= collectionStartDate.Value.Date);
            }
            if (collectionEndDate.HasValue)
            {
                var endDate = collectionEndDate.Value.Date.AddDays(1);
                collectionQuery = collectionQuery.Where(s => s.CreatedDate < endDate);
            }

            var collectionRecords = await collectionQuery
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();

            // ---------- COMPLAINT RECORDS ----------
            // All complaints become permanent records once submitted
            var complaintQuery = _context.Complaint
                .Include(c => c.Sitio)
                .AsQueryable();

            // Apply search filter for complaint records
            if (!string.IsNullOrEmpty(complaintSearch))
            {
                complaintSearch = complaintSearch.Trim();
                complaintQuery = complaintQuery.Where(c =>
                    EF.Functions.Like(c.TicketNumber, $"%{complaintSearch}%") ||
                    EF.Functions.Like(c.ResidentName, $"%{complaintSearch}%") ||
                    EF.Functions.Like(c.ComplaintType, $"%{complaintSearch}%") ||
                    EF.Functions.Like(c.Details, $"%{complaintSearch}%")
                );
            }

            // Apply status filter for complaint records
            if (!string.IsNullOrEmpty(complaintStatus))
            {
                complaintQuery = complaintQuery.Where(c => c.Status == complaintStatus);
            }

            // Apply date range filter for complaint records
            if (complaintStartDate.HasValue)
            {
                complaintQuery = complaintQuery.Where(c => c.FiledDate >= complaintStartDate.Value.Date);
            }
            if (complaintEndDate.HasValue)
            {
                var endDate = complaintEndDate.Value.Date.AddDays(1);
                complaintQuery = complaintQuery.Where(c => c.FiledDate < endDate);
            }

            var complaintRecords = await complaintQuery
                .OrderByDescending(c => c.FiledDate)
                .ToListAsync();

            // Store filter values for the view
            ViewBag.CollectionSearch = collectionSearch;
            ViewBag.CollectionStatus = collectionStatus;
            ViewBag.CollectionStartDate = collectionStartDate;
            ViewBag.CollectionEndDate = collectionEndDate;
            ViewBag.ComplaintSearch = complaintSearch;
            ViewBag.ComplaintStatus = complaintStatus;
            ViewBag.ComplaintStartDate = complaintStartDate;
            ViewBag.ComplaintEndDate = complaintEndDate;

            // Create ViewModel to pass both datasets to the view
            var viewModel = new RecordsManagementViewModel
            {
                CollectionRecords = collectionRecords,
                ComplaintRecords = complaintRecords,
                // Count only Completed and Delayed for collection count
                CollectionCount = await _context.CollectionSchedule
                    .CountAsync(s => s.Status == "Completed" || s.Status == "Delayed"),
                ComplaintCount = await _context.Complaint.CountAsync()
            };

            return View(viewModel);
        }

        // GET: Records/CollectionDetails/5
        // Displays detailed information about a specific collection schedule.
        // Read only - no edit/delete functionality.
        public async Task<IActionResult> CollectionDetails(int id)
        {
            // Retrieve the selected schedule with all related data
            var schedule = await _context.CollectionSchedule
                .Include(s => s.GarbageTruck)
                .Include(s => s.Driver)
                .Include(s => s.CollectionScheduleCollectors).ThenInclude(csc => csc.Collector)
                .Include(s => s.CollectionScheduleSitios).ThenInclude(css => css.Sitio)
                .FirstOrDefaultAsync(s => s.CollectionScheduleId == id);

            if (schedule == null)
            {
                return NotFound();
            }

            // Retrieve monitoring history for this schedule
            var monitoringHistory = await _context.MonitoringLog
                .Where(m => m.CollectionScheduleId == id)
                .OrderByDescending(m => m.LogDate)
                .ThenByDescending(m => m.CollectionMonitoringId)
                .ToListAsync();

            ViewBag.MonitoringHistory = monitoringHistory;

            return View(schedule);
        }

        // GET: Records/ComplaintDetails/5
        // Displays detailed information about a specific complaint.
        // Read only - no edit/delete functionality.
        public async Task<IActionResult> ComplaintDetails(int id)
        {
            // Retrieve the selected complaint with Sitio data
            var complaint = await _context.Complaint
                .Include(c => c.Sitio)
                .FirstOrDefaultAsync(c => c.ComplaintId == id);

            if (complaint == null)
            {
                return NotFound();
            }

            return View(complaint);
        }

        // Releases the database context when the controller is disposed.
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}