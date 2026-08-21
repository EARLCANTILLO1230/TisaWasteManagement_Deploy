// Controllers/ReportsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Data;
using TisaWasteManagement.Helpers;
using TisaWasteManagement.Models;
using TisaWasteManagement.Services;

namespace TisaWasteManagement.Controllers
{
    // Both Admin and Inspector are allowed to generate reports.
    [RequireStaffRole("Admin", "Inspector")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IReportGenerator _reportGenerator;

        public ReportsController(ApplicationDbContext context, IReportGenerator reportGenerator)
        {
            _context = context;
            _reportGenerator = reportGenerator;
        }

        // GET: Reports  -> shows the report generation form
        public async Task<IActionResult> Index()
        {
            var model = new ReportViewModel();
            await LoadDropdowns(model);
            return View(model);
        }

        // POST: Reports/Generate -> builds the report and sends the file back to the browser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate(ReportViewModel model)
        {
            // Basic validation: model attributes (Required, etc.)
            if (!ModelState.IsValid)
            {
                await LoadDropdowns(model);
                return View("Index", model);
            }

            // Extra validation: start date can't be after end date
            if (model.StartDate.HasValue && model.EndDate.HasValue && model.EndDate < model.StartDate)
            {
                ModelState.AddModelError("", "Start date cannot be after end date.");
                await LoadDropdowns(model);
                return View("Index", model);
            }

            byte[] reportBytes;
            string fileName;
            string contentType;

            if (model.ReportType == "Collection")
            {
                var data = await BuildCollectionReportData(model);

                if (data.TotalCount == 0)
                {
                    ModelState.AddModelError("", "No data found for the selected criteria.");
                    await LoadDropdowns(model);
                    return View("Index", model);
                }

                reportBytes = _reportGenerator.GenerateCollectionReport(data, model.ExportFormat);
                (fileName, contentType) = BuildFileNameAndContentType("CollectionReport", model.ExportFormat);
            }
            else
            {
                var data = await BuildComplaintReportData(model);

                if (data.TotalComplaints == 0)
                {
                    ModelState.AddModelError("", "No data found for the selected criteria.");
                    await LoadDropdowns(model);
                    return View("Index", model);
                }

                reportBytes = _reportGenerator.GenerateComplaintReport(data, model.ExportFormat);
                (fileName, contentType) = BuildFileNameAndContentType("ComplaintReport", model.ExportFormat);
            }

            // File() sends the bytes to the browser, which downloads them as fileName
            return File(reportBytes, contentType, fileName);
        }

        // ---------- Query + build the Collection Summary data ----------

        private async Task<CollectionReportData> BuildCollectionReportData(ReportViewModel model)
        {
            // We report on CollectionScheduleSitio because that's where the
            // per-sitio Status (Pending/Completed/Delayed) actually lives.
            var query = _context.CollectionScheduleSitio
                .Include(css => css.CollectionSchedule)
                .Include(css => css.Sitio)
                .AsQueryable();

            if (model.StartDate.HasValue)
            {
                query = query.Where(css => css.CollectionSchedule.CreatedDate >= model.StartDate.Value.Date);
            }
            if (model.EndDate.HasValue)
            {
                var endDateExclusive = model.EndDate.Value.Date.AddDays(1);
                query = query.Where(css => css.CollectionSchedule.CreatedDate < endDateExclusive);
            }
            if (!string.IsNullOrEmpty(model.StatusFilter))
            {
                query = query.Where(css => css.Status == model.StatusFilter);
            }
            if (model.SitioId.HasValue)
            {
                query = query.Where(css => css.SitioId == model.SitioId.Value);
            }

            var records = await query.ToListAsync();

            var data = new CollectionReportData
            {
                GeneratedBy = HttpContext.Session.GetString("StaffRole") ?? "Staff",
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                TotalCount = records.Count,
                CompletedCount = records.Count(r => r.Status == "Completed"),
                DelayedCount = records.Count(r => r.Status == "Delayed"),
                PendingCount = records.Count(r => r.Status == "Pending")
            };

            // Group the records by sitio name to build the per-sitio breakdown
            data.SitioSummaries = records
                .GroupBy(r => r.Sitio.SitioName)
                .Select(g => new SitioCollectionSummary
                {
                    SitioName = g.Key,
                    Total = g.Count(),
                    Completed = g.Count(r => r.Status == "Completed"),
                    Delayed = g.Count(r => r.Status == "Delayed"),
                    Pending = g.Count(r => r.Status == "Pending")
                })
                .OrderByDescending(s => s.Total)
                .ToList();

            // Group by month (using the schedule's CreatedDate) to build the trend list.
            // Records with no CreatedDate are skipped since they can't be placed on a timeline.
            data.Trends = records
                .Where(r => r.CollectionSchedule.CreatedDate.HasValue)
                .GroupBy(r => new
                {
                    r.CollectionSchedule.CreatedDate!.Value.Year,
                    r.CollectionSchedule.CreatedDate!.Value.Month
                })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new MonthlyCollectionTrend
                {
                    MonthYear = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                    Total = g.Count(),
                    Completed = g.Count(r => r.Status == "Completed"),
                    Delayed = g.Count(r => r.Status == "Delayed"),
                    Pending = g.Count(r => r.Status == "Pending")
                })
                .ToList();

            return data;
        }

        // ---------- Query + build the Complaint Summary data ----------

        private async Task<ComplaintReportData> BuildComplaintReportData(ReportViewModel model)
        {
            var query = _context.Complaint
                .Include(c => c.Sitio)
                .AsQueryable();

            if (model.StartDate.HasValue)
            {
                query = query.Where(c => c.FiledDate >= model.StartDate.Value.Date);
            }
            if (model.EndDate.HasValue)
            {
                var endDateExclusive = model.EndDate.Value.Date.AddDays(1);
                query = query.Where(c => c.FiledDate < endDateExclusive);
            }
            if (!string.IsNullOrEmpty(model.StatusFilter))
            {
                query = query.Where(c => c.Status == model.StatusFilter);
            }
            if (model.SitioId.HasValue)
            {
                query = query.Where(c => c.SitioId == model.SitioId.Value);
            }

            var complaints = await query.ToListAsync();

            var data = new ComplaintReportData
            {
                GeneratedBy = HttpContext.Session.GetString("StaffRole") ?? "Staff",
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                TotalComplaints = complaints.Count,
                AwaitingReviewCount = complaints.Count(c => c.Status == "Awaiting Review"),
                OngoingCount = complaints.Count(c => c.Status == "Ongoing"),
                AccomplishedCount = complaints.Count(c => c.Status == "Accomplished")
            };

            data.ComplaintTypeSummaries = complaints
                .GroupBy(c => c.ComplaintType)
                .Select(g => new ComplaintTypeSummary { ComplaintType = g.Key, Count = g.Count() })
                .OrderByDescending(t => t.Count)
                .ToList();

            data.SitioSummaries = complaints
                .GroupBy(c => c.Sitio != null ? c.Sitio.SitioName : "Unknown")
                .Select(g => new SitioComplaintSummary
                {
                    SitioName = g.Key,
                    Total = g.Count(),
                    AwaitingReview = g.Count(c => c.Status == "Awaiting Review"),
                    Ongoing = g.Count(c => c.Status == "Ongoing"),
                    Accomplished = g.Count(c => c.Status == "Accomplished"),
                    PercentageOfTotal = complaints.Count > 0 ? (double)g.Count() / complaints.Count * 100 : 0
                })
                .OrderByDescending(s => s.Total)
                .ToList();

            // Group by month (using FiledDate) to build the trend list, oldest to newest.
            var monthlyTrends = complaints
                .GroupBy(c => new { c.FiledDate.Year, c.FiledDate.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new MonthlyTrend
                {
                    MonthYear = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                    Complaints = g.Count()
                })
                .ToList();

            // Work out the % change from each month to the next (skip the first
            // month - there's nothing before it to compare against).
            for (int i = 1; i < monthlyTrends.Count; i++)
            {
                var previousCount = monthlyTrends[i - 1].Complaints;
                if (previousCount > 0)
                {
                    monthlyTrends[i].PercentChangeFromPreviousMonth =
                        (double)(monthlyTrends[i].Complaints - previousCount) / previousCount * 100;
                }
            }
            data.Trends = monthlyTrends;

            return data;
        }

        // ---------- Helpers ----------

        // Builds the downloaded file's name and MIME type based on the chosen export format
        private (string fileName, string contentType) BuildFileNameAndContentType(string reportPrefix, string exportFormat)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            if (exportFormat == "Excel")
            {
                return (
                    $"{reportPrefix}_{timestamp}.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                );
            }

            return ($"{reportPrefix}_{timestamp}.pdf", "application/pdf");
        }

        // Fills in every dropdown list the form needs
        private async Task LoadDropdowns(ReportViewModel model)
        {
            model.ReportTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Collection", Text = "Collection Summary" },
                new SelectListItem { Value = "Complaint", Text = "Complaint Summary" }
            };

            model.CollectionStatusOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "All Status" },
                new SelectListItem { Value = "Completed", Text = "Completed" },
                new SelectListItem { Value = "Delayed", Text = "Delayed" },
                new SelectListItem { Value = "Pending", Text = "Pending" }
            };

            model.ComplaintStatusOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "All Status" },
                new SelectListItem { Value = "Awaiting Review", Text = "Awaiting Review" },
                new SelectListItem { Value = "Ongoing", Text = "Ongoing" },
                new SelectListItem { Value = "Accomplished", Text = "Accomplished" }
            };

            model.SitioOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "All Sitios" }
            };
            var sitios = await _context.Sitio.OrderBy(s => s.SitioName).ToListAsync();
            foreach (var sitio in sitios)
            {
                model.SitioOptions.Add(new SelectListItem
                {
                    Value = sitio.SitioId.ToString(),
                    Text = sitio.SitioName
                });
            }

            model.ExportFormats = new List<SelectListItem>
            {
                new SelectListItem { Value = "PDF", Text = "PDF" },
                new SelectListItem { Value = "Excel", Text = "Excel" }
            };
        }
    }
}
