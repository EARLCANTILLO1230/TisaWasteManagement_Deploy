using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Data;
using TisaWasteManagement.Helpers;
using TisaWasteManagement.Models;

namespace TisaWasteManagement.Controllers
{
    [RequireStaffRole("Admin", "Inspector")]
    public class CollectionMonitoringController : Controller
    {
        // Database context used to access application data.
        private readonly ApplicationDbContext _context;

        // Constructor - inject database context.
        public CollectionMonitoringController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: CollectionMonitoring
        // Displays all collection schedules with optional search and day filter functionality.
        public async Task<IActionResult> Index(string search, string statusFilter = "All", string dayFilter = null, int page = 1, DateTime? startDate = null, DateTime? endDate = null)
        {
            // Load schedules together with their related Sitios, Collector, and Garbage Truck information.
            var schedules = _context.CollectionSchedule
                .Include(s => s.CollectionScheduleSitios).ThenInclude(css => css.Sitio)
                .Include(s => s.Driver)
                .Include(s => s.GarbageTruck)
                .AsQueryable();

            // Filter schedules by status if a specific status (other than "All") was selected.
            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                schedules = schedules.Where(s => s.Status == statusFilter);
            }

            // Filter by the collection day assigned to each schedule.
            if (!string.IsNullOrEmpty(dayFilter) && Enum.TryParse<DayOfWeek>(dayFilter, out var parsedDay))
            {
                schedules = schedules.Where(s => s.DayOfWeek == parsedDay);
            }

            // Apply search filter if the user entered a keyword.
            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                // Search by sitio, collector(s), truck, status, notes, or remarks.
                schedules = schedules.Where(s =>
                    (s.CollectionScheduleSitios.Any(css => EF.Functions.Like(css.Sitio.SitioName, $"%{search}%"))) ||
                    (s.Driver != null && (
                        EF.Functions.Like(s.Driver.FirstName, $"%{search}%") ||
                        EF.Functions.Like(s.Driver.LastName, $"%{search}%") ||
                        EF.Functions.Like(s.Driver.FirstName + " " + s.Driver.LastName, $"%{search}%"))) ||
                    (s.GarbageTruck != null && EF.Functions.Like(s.GarbageTruck.PlateNumber, $"%{search}%")) ||
                    (s.GarbageTruck != null && EF.Functions.Like(s.GarbageTruck.MVFileNumber, $"%{search}%")) ||
                    (s.Status != null && EF.Functions.Like(s.Status, $"%{search}%")) ||
                    (s.Note != null && EF.Functions.Like(s.Note, $"%{search}%")) ||
                    _context.MonitoringLog.Any(m =>
                        m.CollectionScheduleId == s.CollectionScheduleId &&
                        m.Remarks != null &&
                        EF.Functions.Like(m.Remarks, $"%{search}%"))
                );
            }

            // Order schedules by custom status priority and then by weekday order (Mon..Sun), then by creation date.
            schedules = schedules
                .OrderBy(s => s.Status == "Pending" ? 0 : s.Status == "Delayed" ? 1 : s.Status == "Completed" ? 3 : 2)
                .ThenBy(s => s.DayOfWeek.HasValue ? ((int)s.DayOfWeek + 6) % 7 : 8) // Map Monday=0..Sunday=6
                .ThenByDescending(s => s.CreatedDate);

            var scheduleList = await schedules.ToListAsync();

            // Collect all schedule IDs for retrieving their latest monitoring logs.
            var scheduleIds = scheduleList.Select(s => s.CollectionScheduleId).ToList();

            // Retrieve only the most recent monitoring log for each schedule.
            var latestLogs = await _context.MonitoringLog
                .Where(m => scheduleIds.Contains(m.CollectionScheduleId))
                .GroupBy(m => m.CollectionScheduleId)
                .Select(g => g.OrderByDescending(m => m.LogDate)
                              .ThenByDescending(m => m.CollectionMonitoringId)
                              .First())
                .ToListAsync();

            // Store the latest remarks, search value, status and day filter for use in the view.
            ViewBag.LatestRemarks = latestLogs.ToDictionary(l => l.CollectionScheduleId, l => l.Remarks);

            // Sort schedules by their next occurrence date (closest upcoming first).
            // If multiple schedules fall on the same date, preserve secondary ordering
            // using status priority and CreatedDate.
            var ordered = scheduleList
                .OrderBy(s => WeeklyRecurrenceHelper.GetNextOccurrenceDate(s))
                .ThenBy(s => WeeklyRecurrenceHelper.GetStatusPriority(s.Status))
                .ThenByDescending(s => s.CreatedDate)
                .ToList();

            // Preserve filter values for redisplaying in the view.
            // NOTE: these used to sit after an early "return View(ordered)" below and
            // were therefore dead code - the search box and filter dropdowns never
            // reflected the user's selection. Moved above the return to actually run.
            ViewBag.Search = search;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.DayFilter = dayFilter;

            // Load available schedule statuses for the status filter dropdown (reuse logic from CollectionScheduleController)
            ViewBag.Statuses = await _context.CollectionSchedule
                .Where(s => s.Status != null)
                .Select(s => s.Status)
                .Distinct()
                .ToListAsync();

            // If the user typed the range backwards (end date before start date),
            // just swap them instead of showing an empty/broken result.
            if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            // Optional date range filter (e.g. "show me Jan 1 - Jul 31").
            // This runs in-memory, same as the sorting above, because it depends on
            // GetDisplayDate() which can't be translated into a SQL query.
            if (startDate.HasValue || endDate.HasValue)
            {
                ordered = ordered.Where(s =>
                {
                    var displayDate = WeeklyRecurrenceHelper.GetDisplayDate(s).Date;
                    if (startDate.HasValue && displayDate < startDate.Value.Date) return false;
                    if (endDate.HasValue && displayDate > endDate.Value.Date) return false;
                    return true;
                }).ToList();
            }

            // Preserve the date range filter values so the view can redisplay them
            // in the form and carry them along in the pagination links.
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            // Once a specific date range is requested, showing everything on one page
            // (sorted oldest to newest) is more useful than paging through mostly-empty
            // weeks to find the handful of matches. So: date range given => one page,
            // no weekly split. No date range => keep the original weekly pagination.
            bool isDateRangeMode = startDate.HasValue || endDate.HasValue;
            ViewBag.IsDateRangeMode = isDateRangeMode;

            List<CollectionSchedule> pagedSchedules;

            if (isDateRangeMode)
            {
                // "ordered" is already sorted with the closest/earliest date first,
                // so it's already in the oldest-to-newest order we want here.
                pagedSchedules = ordered;
                ViewBag.CurrentPage = 1;
                ViewBag.TotalPages = 1;
            }
            else
            {
                // --- Pagination (by week) ---
                // Each page shows one Monday-Sunday week of schedules. Page 1 is
                // always the CURRENT week (the week today falls in), and each next
                // page moves one week forward through however far the data goes.
                var currentWeekStart = GetWeekStart(DateTime.Today);

                // Find the latest week that actually has a schedule in it, so we
                // know how many "next" pages to allow.
                var latestWeekStart = currentWeekStart;
                foreach (var s in ordered)
                {
                    var weekStart = GetWeekStart(WeeklyRecurrenceHelper.GetDisplayDate(s));
                    if (weekStart > latestWeekStart)
                    {
                        latestWeekStart = weekStart;
                    }
                }

                int totalPages = ((latestWeekStart - currentWeekStart).Days / 7) + 1;
                if (totalPages < 1) totalPages = 1; // always show at least 1 page, even if empty

                // Keep the requested page number within a valid range.
                if (page < 1) page = 1;
                if (page > totalPages) page = totalPages;

                // The Monday-Sunday range for the page being displayed.
                var pageWeekStart = currentWeekStart.AddDays((page - 1) * 7);
                var pageWeekEnd = pageWeekStart.AddDays(6);

                // Only keep schedules whose display date falls inside this page's week.
                pagedSchedules = ordered
                    .Where(s => GetWeekStart(WeeklyRecurrenceHelper.GetDisplayDate(s)) == pageWeekStart)
                    .ToList();

                // Pass paging info to the view so it can render page numbers / Next /
                // Previous links, and show the date range being displayed (e.g. "Jul 20 - Jul 26, 2026").
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.WeekStart = pageWeekStart;
                ViewBag.WeekEnd = pageWeekEnd;
            }

            return View(pagedSchedules);
        }

        // Returns the Monday of the Monday-Sunday week that "date" falls in.
        // Used to group schedules into weekly pages for the Index view.
        private static DateTime GetWeekStart(DateTime date)
        {
            date = date.Date;
            // DayOfWeek: Sunday = 0, Monday = 1, ..., Saturday = 6.
            // This converts it so Monday = 0 days back, ..., Sunday = 6 days back.
            int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-daysSinceMonday);
        }

        // GET: CollectionMonitoring/Details/5
        // Displays full information about a collection schedule, including
        // its Note and the full history of monitoring log remarks.
        public async Task<IActionResult> Details(int id)
        {
            // Retrieve the selected schedule together with related information.
            var schedule = await _context.CollectionSchedule
                .Include(s => s.CollectionScheduleSitios).ThenInclude(css => css.Sitio)
                .Include(s => s.Driver)
                .Include(s => s.GarbageTruck)
                .FirstOrDefaultAsync(s => s.CollectionScheduleId == id);

            // Return a 404 page if the schedule does not exist.
            if (schedule == null)
                return NotFound();

            // Retrieve the full monitoring log history for this schedule,
            // most recent entry first.
            var logs = await _context.MonitoringLog
                .Where(m => m.CollectionScheduleId == id)
                .OrderByDescending(m => m.LogDate)
                .ThenByDescending(m => m.CollectionMonitoringId)
                .ToListAsync();

            ViewBag.MonitoringLogs = logs;

            return View(schedule);
        }

        // GET: CollectionMonitoring/Edit/5
        // Displays the sitio collection checklist for the selected collection schedule.
        public async Task<IActionResult> Edit(int id)
        {
            // Retrieve the selected schedule together with related information.
            var schedule = await _context.CollectionSchedule
                .Include(s => s.CollectionScheduleSitios).ThenInclude(css => css.Sitio)
                .Include(s => s.Driver)
                .Include(s => s.GarbageTruck)
                .FirstOrDefaultAsync(s => s.CollectionScheduleId == id);

            // Return a 404 page if the schedule does not exist.
            if (schedule == null)
                return NotFound();

            // Retrieve the latest remark so the textarea starts pre-filled with it.
            var latestLog = await _context.MonitoringLog
                .Where(m => m.CollectionScheduleId == id)
                .OrderByDescending(m => m.LogDate)
                .ThenByDescending(m => m.CollectionMonitoringId)
                .FirstOrDefaultAsync();
            ViewBag.CurrentRemarks = latestLog?.Remarks ?? string.Empty;

            return View(schedule);
        }

        // POST: CollectionMonitoring/Edit/5
        // Saves each assigned sitio's Completed/Delayed status (with a required reason
        // when Delayed), and optionally logs a remark. SitioStatuses/SitioDelayReasons
        // are keyed by CollectionScheduleSitioId, matching the form field names
        // "SitioStatuses[<id>]" and "SitioDelayReasons[<id>]" in the Edit view.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Dictionary<int, string>? SitioStatuses, Dictionary<int, string>? SitioDelayReasons, string? remarks)
        {
            // Retrieve the selected schedule together with its assigned sitios.
            var schedule = await _context.CollectionSchedule
                .Include(s => s.CollectionScheduleSitios).ThenInclude(css => css.Sitio)
                .FirstOrDefaultAsync(s => s.CollectionScheduleId == id);

            if (schedule == null)
                return NotFound();

            SitioStatuses ??= new Dictionary<int, string>();
            SitioDelayReasons ??= new Dictionary<int, string>();

            // Every ACTIVE sitio (not already carried over to another schedule) must
            // have exactly one status selected (Completed or Delayed), and every
            // "Delayed" pick must come with a non-empty reason. Reassigned sitios are
            // skipped - they're no longer this schedule's responsibility, so the form
            // doesn't show a status picker for them. Validate everything up front
            // before changing anything, so a bad submission doesn't partially save.
            var activeSitios = schedule.CollectionScheduleSitios
                .Where(cs => cs.ReassignedToScheduleId == null)
                .ToList();

            foreach (var scheduleSitio in activeSitios)
            {
                if (!SitioStatuses.TryGetValue(scheduleSitio.CollectionScheduleSitioId, out var pickedStatus) ||
                    (pickedStatus != "Completed" && pickedStatus != "Delayed"))
                {
                    ModelState.AddModelError(string.Empty, $"Please select Completed or Delayed for {scheduleSitio.Sitio?.SitioName}.");
                }
                else if (pickedStatus == "Delayed" &&
                    (!SitioDelayReasons.TryGetValue(scheduleSitio.CollectionScheduleSitioId, out var reason) || string.IsNullOrWhiteSpace(reason)))
                {
                    ModelState.AddModelError(string.Empty, $"Please enter a reason for delay for {scheduleSitio.Sitio?.SitioName}.");
                }
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));

                var latestLogOnError = await _context.MonitoringLog
                    .Where(m => m.CollectionScheduleId == id)
                    .OrderByDescending(m => m.LogDate)
                    .ThenByDescending(m => m.CollectionMonitoringId)
                    .FirstOrDefaultAsync();
                ViewBag.CurrentRemarks = remarks ?? latestLogOnError?.Remarks ?? string.Empty;

                return View(schedule);
            }

            // Figure out which sitios are NEWLY set to Completed, and which are NEWLY
            // set to Delayed, in THIS submission - i.e. their Status value is about to
            // change. This must be worked out BEFORE the loop below updates Status,
            // otherwise every already-Completed/Delayed sitio would look "changed"
            // again on every later save.
            var newlyCompleted = activeSitios
                .Where(cs => cs.Status != "Completed" && SitioStatuses[cs.CollectionScheduleSitioId] == "Completed")
                .OrderBy(cs => cs.Sitio?.SitioName)
                .ToList();

            var newlyDelayed = activeSitios
                .Where(cs => cs.Status != "Delayed" && SitioStatuses[cs.CollectionScheduleSitioId] == "Delayed")
                .OrderBy(cs => cs.Sitio?.SitioName)
                .ToList();

            foreach (var scheduleSitio in activeSitios)
            {
                var newStatus = SitioStatuses[scheduleSitio.CollectionScheduleSitioId];
                scheduleSitio.Status = newStatus;
                scheduleSitio.ReasonForDelay = newStatus == "Delayed"
                    ? SitioDelayReasons[scheduleSitio.CollectionScheduleSitioId].Trim()
                    : null;
            }

            // Tracks whether ANY log entry was created below (Completed and/or Delayed),
            // so the "nothing changed but a remark was entered" fallback further down
            // knows not to add a redundant extra entry.
            bool anyEntryLogged = false;

            if (newlyCompleted.Any())
            {
                // One or more NEW sitios were marked Completed this time: save ONE
                // merged "Completed" entry covering just those. The remark (if any)
                // is attached here too - a single remark can apply to both a Completed
                // group and a Delayed group in the same save.
                _context.MonitoringLog.Add(new CollectionMonitoring
                {
                    CollectionScheduleId = id,
                    Status = "Completed",
                    SitioNames = string.Join(", ", newlyCompleted.Select(cs => cs.Sitio?.SitioName)),
                    Remarks = remarks,
                    LogDate = DateTime.Now
                });
                anyEntryLogged = true;
            }

            if (newlyDelayed.Any())
            {
                // One or more NEW sitios were marked Delayed this time: save ONE merged
                // "Delayed" entry covering just those. Each sitio can have its own
                // reason, so combine them as "SitioName: reason" pairs. The remark (if
                // any) is attached here too, same as the Completed entry above.
                _context.MonitoringLog.Add(new CollectionMonitoring
                {
                    CollectionScheduleId = id,
                    Status = "Delayed",
                    SitioNames = string.Join(", ", newlyDelayed.Select(cs => cs.Sitio?.SitioName)),
                    ReasonForDelay = string.Join("; ", newlyDelayed.Select(cs => $"{cs.Sitio?.SitioName}: {cs.ReasonForDelay}")),
                    Remarks = remarks,
                    LogDate = DateTime.Now
                });
                anyEntryLogged = true;
            }

            if (!anyEntryLogged && !string.IsNullOrWhiteSpace(remarks))
            {
                // No sitio status actually changed this time, but a remark was
                // entered (e.g. explaining why nothing has changed yet). Log it as-is at
                // the schedule's current status, so it shows up on the Details page's history.
                _context.MonitoringLog.Add(new CollectionMonitoring
                {
                    CollectionScheduleId = id,
                    Status = schedule.Status,
                    Remarks = remarks,
                    LogDate = DateTime.Now
                });
            }


            await _context.SaveChangesAsync();

            TempData["Success"] = "Sitio collection status saved.";
            return RedirectToAction("Index");
        }

        // Date/priority logic now lives in TisaWasteManagement.Helpers.WeeklyRecurrenceHelper
        // (shared with CollectionScheduleController and the Razor views) instead of
        // being duplicated here.
        //
        // NOTE: The "mark schedule Completed" workflow (which used to live here, including
        // weekly-recurrence generation and its ResolveDumpNumberAsync helper) now lives in
        // CollectionScheduleController.UpdateStatus, since the Collection Status dropdown
        // moved to the CollectionSchedule/Details page. This controller's UpdateStatus is
        // now only responsible for the per-sitio collection checklist above.

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
