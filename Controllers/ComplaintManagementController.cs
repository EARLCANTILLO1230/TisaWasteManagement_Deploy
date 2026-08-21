using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TisaWasteManagement.Data;
using TisaWasteManagement.Models;
using TisaWasteManagement.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TisaWasteManagement.Controllers
{
    /// <summary>
    /// Handles complaint management for inspectors/admins.
    /// Allows viewing, filtering, and updating complaint statuses.
    /// </summary>
    [RequireStaffRole("Admin", "Inspector")]
    public class ComplaintManagementController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ComplaintManagementController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET: ComplaintManagement/Index
        /// Displays the complaint management dashboard with list of all complaints.
        /// Includes search, filtering, and statistics.
        /// </summary>
        public async Task<IActionResult> Index(string search, string statusFilter)
        {
            // Start with all complaints including Sitio data
            var query = _context.Complaint
                .Include(c => c.Sitio)
                .AsQueryable();

            // Apply status filter if provided
            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(c => c.Status == statusFilter);
            }

            // Apply search filter if provided
            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                query = query.Where(c =>
                    c.TicketNumber.Contains(search) ||
                    c.ResidentName.Contains(search) ||
                    c.ContactNumber.Contains(search) ||
                    c.ComplaintType.Contains(search) ||
                    c.Status.Contains(search) ||
                    (c.Sitio != null && c.Sitio.SitioName.Contains(search)) ||
                    c.Details.Contains(search)
                );
            }

            // Order by newest first
            query = query.OrderByDescending(c => c.FiledDate);

            // Get the list of complaints
            var complaints = await query.ToListAsync();

            // Build the ViewModel
            var viewModel = new ComplaintManagementViewModel
            {
                Complaints = complaints,
                SearchKeyword = search ?? string.Empty,
                StatusFilter = statusFilter ?? string.Empty,
                Statistics = new ComplaintStatistics
                {
                    TotalCount = await _context.Complaint.CountAsync(),
                    AwaitingReviewCount = await _context.Complaint.CountAsync(c => c.Status == "Awaiting Review"), // Count complaints still awaiting review
                    OngoingCount = await _context.Complaint.CountAsync(c => c.Status == "Ongoing"),
                    AccomplishedCount = await _context.Complaint.CountAsync(c => c.Status == "Accomplished")
                }
            };

            // Build status filter dropdown
            ViewBag.StatusOptions = new SelectList(
                new[]
                {
                    new { Value = "", Text = "All Statuses" },
                    new { Value = "Awaiting Review", Text = "Awaiting Review" },
                    new { Value = "Ongoing", Text = "Ongoing" },
                    new { Value = "Accomplished", Text = "Accomplished" }
                },
                "Value",
                "Text",
                statusFilter
            );

            return View(viewModel);
        }

        /// <summary>
        /// GET: ComplaintManagement/Details/5
        /// Displays full details of a specific complaint.
        /// </summary>
        public async Task<IActionResult> Details(int? id)
        {
            if (!id.HasValue)
            {
                TempData["Error"] = "Invalid complaint id.";
                return RedirectToAction("Index");
            }

            var complaint = await _context.Complaint
                .Include(c => c.Sitio)
                .FirstOrDefaultAsync(c => c.ComplaintId == id.Value);

            if (complaint == null)
            {
                TempData["Error"] = "Complaint not found.";
                return RedirectToAction("Index");
            }

            return View(complaint);
        }

        /// <summary>
        /// GET: ComplaintManagement/Resolve/5
        /// Displays the form to update a specific complaint's status.
        /// </summary>
        public async Task<IActionResult> Resolve(int? id)
        {
            if (!id.HasValue)
            {
                TempData["Error"] = "Invalid complaint id.";
                return RedirectToAction("Index");
            }

            var complaint = await _context.Complaint
                .Include(c => c.Sitio)
                .FirstOrDefaultAsync(c => c.ComplaintId == id.Value);

            if (complaint == null)
            {
                TempData["Error"] = "Complaint not found.";
                return RedirectToAction("Index");
            }

            // Build the ViewModel
            var viewModel = new ResolveComplaintViewModel
            {
                ComplaintId = complaint.ComplaintId,
                Complaint = complaint,
                SelectedStatus = complaint.Status,
                StatusOptions = new List<StatusOption>
                {
                    new StatusOption { Value = "Ongoing", Text = "Ongoing - Being Addressed" },
                    new StatusOption { Value = "Accomplished", Text = "Accomplished - Resolved" }
                }
            };

            return View(viewModel);
        }

        /// <summary>
        /// POST: ComplaintManagement/Resolve
        /// Updates the status of a complaint.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(ResolveComplaintViewModel viewModel)
        {
            // Validate that a status was selected
            if (string.IsNullOrEmpty(viewModel.SelectedStatus))
            {
                TempData["Error"] = "Please select a status.";

                // Reload the complaint data
                var complaintData = await _context.Complaint
                    .Include(c => c.Sitio)
                    .FirstOrDefaultAsync(c => c.ComplaintId == viewModel.ComplaintId);

                if (complaintData == null)
                {
                    return RedirectToAction("Index");
                }

                viewModel.Complaint = complaintData;
                viewModel.StatusOptions = new List<StatusOption>
                {
                    new StatusOption { Value = "Ongoing", Text = "Ongoing - Being Addressed" },
                    new StatusOption { Value = "Accomplished", Text = "Accomplished - Resolved" }
                };

                return View(viewModel);
            }

            // Find the complaint
            var complaintToUpdate = await _context.Complaint.FindAsync(viewModel.ComplaintId);
            if (complaintToUpdate == null)
            {
                TempData["Error"] = "Complaint not found.";
                return RedirectToAction("Index");
            }

            // Validate status is either Ongoing or Accomplished
            var validStatuses = new[] { "Ongoing", "Accomplished" };
            if (!validStatuses.Contains(viewModel.SelectedStatus))
            {
                TempData["Error"] = "Invalid status value.";
                return RedirectToAction("Index");
            }

            // Remember the old status so we can tell it actually changed
            var oldStatus = complaintToUpdate.Status;

            // Update the complaint
            complaintToUpdate.Status = viewModel.SelectedStatus;
            complaintToUpdate.UpdatedDate = DateTime.Now;

            _context.Entry(complaintToUpdate).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // ===== SMS Module: notify the resident of the status change =====
            // Only send if: the status actually changed, a contact number exists,
            // and we're not sending on the initial "Awaiting Review" state
            // (SMS is never sent when a complaint is first filed).
            if (oldStatus != viewModel.SelectedStatus &&
                !string.IsNullOrEmpty(complaintToUpdate.ContactNumber) &&
                viewModel.SelectedStatus != "Awaiting Review")
            {
                try
                {
                    var smsService = HttpContext.RequestServices.GetRequiredService<TisaWasteManagement.Services.ISmsService>();
                    var message = $"Barangay Tisa: Your complaint #{complaintToUpdate.TicketNumber} is now {viewModel.SelectedStatus}. Thank you for your patience.";
                    await smsService.SendSmsAsync(complaintToUpdate.ContactNumber, message, "ComplaintUpdate", complaintToUpdate.ComplaintId);
                }
                catch (Exception ex)
                {
                    // If SMS fails for any reason, don't block the status update -
                    // just log it to the console so the Admin/Inspector flow keeps working.
                    Console.WriteLine($"SMS sending failed: {ex.Message}");
                }
            }

            TempData["Success"] = $"Complaint #{complaintToUpdate.TicketNumber} status updated to {viewModel.SelectedStatus} successfully!";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// GET: ComplaintManagement/GetComplaintCounts
        /// API endpoint to get updated complaint counts (for AJAX refresh)
        /// </summary>
        public async Task<IActionResult> GetComplaintCounts()
        {
            var statistics = new ComplaintStatistics
            {
                TotalCount = await _context.Complaint.CountAsync(),
                AwaitingReviewCount = await _context.Complaint.CountAsync(c => c.Status == "Awaiting Review"), // Count complaints still awaiting review
                OngoingCount = await _context.Complaint.CountAsync(c => c.Status == "Ongoing"),
                AccomplishedCount = await _context.Complaint.CountAsync(c => c.Status == "Accomplished")
            };

            return Json(statistics);
        }

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