using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Data;
using TisaWasteManagement.Helpers;
using TisaWasteManagement.Models;

namespace TisaWasteManagement.Controllers
{
    // Controller responsible for managing collection schedules.
    // Provides functionality for creating, editing, searching,
    // viewing, and deleting collection schedules.
    // Restricted to Admin only - EXCEPT PublicIndex, which is marked
    // [AllowAnonymous] below so Residents can still view the read-only schedule.
    [RequireStaffRole("Admin")]
    public class CollectionScheduleController : Controller
    {
        // Database context used to access application data.
        private readonly ApplicationDbContext _context;

        // Constructor that injects the application's database context.
        public CollectionScheduleController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Date-calculation logic (CalculateScheduledDate, GetNextOccurrenceDate,
        // GetStatusPriority) now lives in TisaWasteManagement.Helpers.WeeklyRecurrenceHelper
        // so it isn't duplicated across this controller, CollectionMonitoringController,
        // and the Razor views. See that class for the rules.

        // List & Search
        // Displays all collection schedules with optional search
        // and status filtering.
        public async Task<IActionResult> Index(string search, string statusFilter = "All", bool showAll = false, string dayFilter = null, int page = 1, DateTime? startDate = null, DateTime? endDate = null)
        {
            // Load collection schedules together with their related
            // Sitios (via join), Driver, Collectors (via join), and Garbage Truck information.
            var schedules = _context.CollectionSchedule
                .Include(s => s.CollectionScheduleSitios).ThenInclude(css => css.Sitio)
                .Include(s => s.Driver)
                .Include(s => s.CollectionScheduleCollectors).ThenInclude(csc => csc.Collector)
                .Include(s => s.GarbageTruck)
                .AsQueryable();

            // Filter schedules by status if a specific status (other than "All") was selected.
            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                schedules = schedules.Where(s => s.Status == statusFilter);
            }

            // Filter schedules by their assigned collection day.
            if (!string.IsNullOrEmpty(dayFilter) && Enum.TryParse<DayOfWeek>(dayFilter, out var parsedDay))
            {
                schedules = schedules.Where(s => s.DayOfWeek == parsedDay);
            }

            // Perform keyword search if a search value is provided.
            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                // Search by sitio, driver, collector(s), truck, or note.
                schedules = schedules.Where(s =>
                    (s.CollectionScheduleSitios.Any(css => EF.Functions.Like(css.Sitio.SitioName, $"%{search}%"))) ||
                    (s.Driver != null && (
                        EF.Functions.Like(s.Driver.FirstName, $"%{search}%") ||
                        EF.Functions.Like(s.Driver.LastName, $"%{search}%") ||
                        EF.Functions.Like(s.Driver.FirstName + " " + s.Driver.LastName, $"%{search}%"))) ||
                    (s.CollectionScheduleCollectors.Any(csc =>
                        EF.Functions.Like(csc.Collector.FirstName, $"%{search}%") ||
                        EF.Functions.Like(csc.Collector.LastName, $"%{search}%") ||
                        EF.Functions.Like(csc.Collector.FirstName + " " + csc.Collector.LastName, $"%{search}%"))) ||
                    (s.GarbageTruck != null && EF.Functions.Like(s.GarbageTruck.PlateNumber, $"%{search}%")) ||
                    (s.GarbageTruck != null && EF.Functions.Like(s.GarbageTruck.MVFileNumber, $"%{search}%")) ||
                    (s.Note != null && EF.Functions.Like(s.Note, $"%{search}%")) ||
                    false
                );
            }

            // Order schedules by custom status priority and then by weekday order (Mon..Sun), then by creation date.
            schedules = schedules
                .OrderBy(s => s.Status == "Pending" ? 0 : s.Status == "Delayed" ? 1 : s.Status == "Completed" ? 3 : 2)
                .ThenBy(s => s.DayOfWeek.HasValue ? ((int)s.DayOfWeek + 6) % 7 : 8) // Map Monday=0..Sunday=6
                .ThenByDescending(s => s.CreatedDate);

            // Preserve filter values for redisplaying in the view.
            ViewBag.Search = search;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.DayFilter = dayFilter;

            // Load available schedule statuses for the filter dropdown.
            ViewBag.Statuses = await _context.CollectionSchedule
                .Where(s => s.Status != null)
                .Select(s => s.Status)
                .Distinct()
                .ToListAsync();

            // Execute the query and retrieve the schedules.
            var scheduleList = await schedules.ToListAsync();

            // Pull each schedule's most recent monitoring log remark for display
            // in the Collection Schedule index. This mirrors the logic used in
            // the CollectionMonitoringController so users can see the latest
            // field verification remark next to the schedule.
            var scheduleIds = scheduleList.Select(s => s.CollectionScheduleId).ToList();

            // Retrieve only the latest monitoring log for every schedule.
            var latestLogs = await _context.MonitoringLog
                .Where(m => scheduleIds.Contains(m.CollectionScheduleId))
                .GroupBy(m => m.CollectionScheduleId)
                .Select(g => g.OrderByDescending(m => m.LogDate)
                              .ThenByDescending(m => m.CollectionMonitoringId)
                              .First())
                .ToListAsync();

            // Store the latest remarks so they can be displayed in the view.
            ViewBag.LatestRemarks = latestLogs.ToDictionary(l => l.CollectionScheduleId, l => l.Remarks);

            // Sort schedules by the next occurrence date (closest upcoming first).
            // For schedules that share the same date, preserve a secondary sort order
            // based on the existing status priority and then by CreatedDate (desc).
            var ordered = scheduleList
                .OrderBy(s => WeeklyRecurrenceHelper.GetNextOccurrenceDate(s))
                .ThenBy(s => WeeklyRecurrenceHelper.GetStatusPriority(s.Status))
                .ThenByDescending(s => s.CreatedDate)
                .ToList();

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

        // GET: Create
        // Displays the form for creating a new collection schedule.
        public IActionResult Create()
        {
            // No selected status for a new schedule
            LoadDropdowns();
            return View();
        }

        // POST: Create
        // Saves a newly created collection schedule.
        // "schedule" holds the basic fields typed in the form (Status, Note, DayOfWeek, DumpNumber).
        // SelectedSitioIds and SelectedCollectorIds are multi-select lists (a schedule can cover
        // several Sitios and have several Collectors), but SelectedDriverId and
        // SelectedGarbageTruckId are single values - a schedule has exactly one driver and
        // exactly one garbage truck.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Status,Note,RepeatWeekly,DumpNumber")] CollectionSchedule schedule, List<int> SelectedSitioIds, int? SelectedDriverId, List<int> SelectedCollectorIds, int? SelectedGarbageTruckId, List<DayOfWeek> SelectedDays)
        {
            // Load dropdowns and pre-select the status the user posted (if any)
            LoadDropdowns(schedule.Status);
            SelectedCollectorIds ??= new List<int>();

            // Basic model validation check
            if (SelectedSitioIds == null || !SelectedSitioIds.Any())
            {
                ModelState.AddModelError("Sitio", "Please select at least one Sitio.");
            }
            if (SelectedGarbageTruckId == null)
            {
                ModelState.AddModelError("GarbageTruck", "Please select a Garbage Truck.");
            }

            // Require a driver be assigned to the schedule
            if (SelectedDriverId == null)
            {
                ModelState.AddModelError("Driver", "Please select a driver.");
            }

            // Require at least one collector be assigned to the schedule
            if (!SelectedCollectorIds.Any())
            {
                ModelState.AddModelError("Collector", "Please select at least one collector.");
            }

            // Ensure at least one day was selected when creating schedules
            SelectedDays ??= new List<DayOfWeek>();
            if (!SelectedDays.Any())
            {
                ModelState.AddModelError("SelectedDays", "Please select at least one collection day.");
            }

            // Remove model state validation for DayOfWeek because the form now submits
            // multiple days via SelectedDays. We validate SelectedDays separately above.
            ModelState.Remove(nameof(CollectionSchedule.DayOfWeek));

            // Validate the selected day(s) against the driver's and each collector's day off.
            // If any selected day matches one of their configured DaysOff, block the save.
            var daysOffConflict = await FindDaysOffConflictAsync(SelectedDriverId, SelectedCollectorIds, SelectedDays);
            if (daysOffConflict != null)
            {
                ModelState.AddModelError("Collector", daysOffConflict);
                // Preserve selections for redisplay
                ViewBag.SelectedSitioIds = SelectedSitioIds ?? new List<int>();
                ViewBag.SelectedDriverId = SelectedDriverId;
                ViewBag.SelectedCollectorIds = SelectedCollectorIds;
                ViewBag.SelectedGarbageTruckId = SelectedGarbageTruckId;
                ViewBag.SelectedDays = SelectedDays;
                TempData["Error"] = daysOffConflict;
                return View(schedule);
            }

            // Run shared business-rule checks (truck status, duplicate sitios, etc).
            var validationError = await ValidateScheduleAsync(schedule, SelectedSitioIds, SelectedDriverId, SelectedGarbageTruckId, excludeScheduleId: null);

            // Return the form if any business rule validation fails.
            if (validationError != null)
            {
                TempData["Error"] = validationError;
                ViewBag.SelectedSitioIds = SelectedSitioIds ?? new List<int>();
                ViewBag.SelectedDriverId = SelectedDriverId;
                ViewBag.SelectedCollectorIds = SelectedCollectorIds;
                return View(schedule);
            }

            // Validate model properties before saving.
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the highlighted fields.";
                ViewBag.SelectedSitioIds = SelectedSitioIds ?? new List<int>();
                ViewBag.SelectedDriverId = SelectedDriverId;
                ViewBag.SelectedCollectorIds = SelectedCollectorIds;
                ViewBag.SelectedGarbageTruckId = SelectedGarbageTruckId;
                return View(schedule);
            }

            // Create a schedule for each selected day. This keeps the implementation simple by creating
            // separate schedule rows for each day the user selected.
            foreach (var day in SelectedDays.Distinct())
            {
                // Dump Number availability is per truck+day, so it must be resolved
                // separately for EACH day being created here (a number free on Monday
                // might already be taken on Tuesday, and vice versa).
                var resolvedDumpNumber = await ResolveDumpNumberAsync(SelectedGarbageTruckId, day, schedule.DumpNumber, excludeScheduleId: null);

                var newSchedule = new CollectionSchedule
                {
                    Status = schedule.Status,
                    Note = schedule.Note,
                    RepeatWeekly = schedule.RepeatWeekly,
                    DayOfWeek = day,
                    DumpNumber = resolvedDumpNumber,
                    // A schedule now points at one GarbageTruck and one Driver directly.
                    TruckId = SelectedGarbageTruckId,
                    DriverId = SelectedDriverId,
                    // Compute the scheduled occurrence date according to rules:
                    // - If selected weekday == today => today
                    // - If selected weekday after today => date in current week
                    // - If selected weekday before today => date in next week
                    CreatedDate = WeeklyRecurrenceHelper.CalculateScheduledDate(day)
                };
                _context.CollectionSchedule.Add(newSchedule);
                await _context.SaveChangesAsync();

                // Attach join links for the Sitios and Collectors on this new schedule (both multi-select).
                AddScheduleLinks(newSchedule.CollectionScheduleId, SelectedSitioIds);
                AddCollectorLinks(newSchedule.CollectionScheduleId, SelectedCollectorIds);
            }
            await _context.SaveChangesAsync();

            TempData["Success"] = "Schedule(s) saved successfully.";
            return RedirectToAction("Index");
        }

        // GET: Edit
        // Displays the edit form for an existing collection schedule.
        public async Task<IActionResult> Edit(int id)
        {
            // Try to find a schedule by schedule id
            var schedule = await _context.CollectionSchedule
                .Include(s => s.CollectionScheduleSitios).ThenInclude(css => css.Sitio)
                .Include(s => s.Driver)
                .Include(s => s.CollectionScheduleCollectors)
                .Include(s => s.GarbageTruck)
                .FirstOrDefaultAsync(s => s.CollectionScheduleId == id);

            if (schedule?.Status == "Completed")
            {
                // A completed schedule is retained as history. Start a new schedule instead,
                // but keep the same driver/collectors pre-selected since that part of the info is still useful.
                ViewBag.SelectedSitioIds = new List<int>();
                ViewBag.SelectedDriverId = schedule.DriverId;
                ViewBag.SelectedCollectorIds = schedule.CollectionScheduleCollectors.Select(csc => csc.CollectorId).ToList();
                ViewBag.SelectedGarbageTruckId = (int?)null;
                schedule = new CollectionSchedule
                {
                    Status = "Pending",
                    CollectionScheduleSitios = new List<CollectionScheduleSitio>()
                };
            }
            else if (schedule == null)
            {
                // If not found, return 404
                return NotFound();
            }
            else
            {
                // Schedule already exists, so pull out the ids of its related sitios,
                // its single driver, its collectors, and its single truck so the view can show them as already selected.
                ViewBag.SelectedSitioIds = schedule.CollectionScheduleSitios.Select(cs => cs.SitioId).ToList();
                ViewBag.SelectedDriverId = schedule.DriverId;
                ViewBag.SelectedCollectorIds = schedule.CollectionScheduleCollectors.Select(csc => csc.CollectorId).ToList();
                ViewBag.SelectedGarbageTruckId = schedule.TruckId;
            }

            // Ensure dropdowns reflect the schedule being edited (pre-select status).
            // Pass "id" so this schedule's own Sitio/Driver/Collector assignments aren't
            // treated as "already booked" against itself.
            LoadDropdowns(schedule.Status, id);
            ViewBag.SelectedDays = schedule.DayOfWeek.HasValue
                ? new List<DayOfWeek> { schedule.DayOfWeek.Value }
                : new List<DayOfWeek>();

            return View(schedule);
        }

        // POST: Edit
        // Creates or updates a collection schedule. If the schedule id is 0, creates a new schedule for the provided collector id.
        //
        // REFACTORED for readability: this method used to be ~207 lines doing five
        // different jobs in sequence (days-off validation, required-field validation,
        // conflict validation, new-schedule creation, existing-schedule update). It's
        // now just the high-level flow; each job lives in its own private helper below.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CollectionScheduleId,Status,Note,RepeatWeekly,DumpNumber")] CollectionSchedule schedule, List<int> SelectedSitioIds, int? SelectedDriverId, List<int> SelectedCollectorIds, int? SelectedGarbageTruckId, List<DayOfWeek> SelectedDays)
        {
            // If this is an update, ensure the requested record matches the submitted schedule.
            if (schedule.CollectionScheduleId != 0 && id != schedule.CollectionScheduleId)
                return NotFound();

            // Ensure dropdowns show the user's selection if we need to redisplay the form
            LoadDropdowns(schedule.Status, schedule.CollectionScheduleId);
            SelectedDays = (SelectedDays ?? new List<DayOfWeek>()).Distinct().ToList();
            SelectedCollectorIds ??= new List<int>();
            ModelState.Remove(nameof(CollectionSchedule.DayOfWeek));

            if (!SelectedDays.Any())
            {
                ModelState.AddModelError("SelectedDays", "Please select at least one collection day.");
            }
            else
            {
                schedule.DayOfWeek = SelectedDays.First();
            }

            // 1) Days-off check: does the driver, or any selected collector, have a day off on any selected day?
            var daysOffConflict = await FindDaysOffConflictAsync(SelectedDriverId, SelectedCollectorIds, SelectedDays);
            if (daysOffConflict != null)
            {
                ModelState.AddModelError("Collector", daysOffConflict);
                TempData["Error"] = daysOffConflict;
                return EditFormWithErrors(schedule, SelectedSitioIds, SelectedDriverId, SelectedCollectorIds, SelectedGarbageTruckId, SelectedDays);
            }

            // 2) Required-selection checks: sitio, truck, driver, and at least one collector must each have a pick.
            ValidateRequiredSelections(SelectedSitioIds, SelectedGarbageTruckId, SelectedDriverId, SelectedCollectorIds);

            // 3) Per-day conflict check: does any selected day/sitio combination already have
            //    another active schedule? (Skipped automatically for Completed schedules -
            //    see the bug-fix comment inside ValidateScheduleAsync.)
            var conflictError = await ValidateSelectedDaysForConflictsAsync(SelectedDays, schedule.Status, schedule.DumpNumber, SelectedSitioIds, SelectedDriverId, SelectedGarbageTruckId, schedule.CollectionScheduleId);
            if (conflictError != null)
            {
                TempData["Error"] = conflictError;
                return EditFormWithErrors(schedule, SelectedSitioIds, SelectedDriverId, SelectedCollectorIds, SelectedGarbageTruckId, SelectedDays);
            }

            // 4) Standard model validation (required fields, string lengths, etc).
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the highlighted fields.";
                return EditFormWithErrors(schedule, SelectedSitioIds, SelectedDriverId, SelectedCollectorIds, SelectedGarbageTruckId, SelectedDays);
            }

            // 5) A CollectionScheduleId of 0 means this is really a "new" schedule, not an existing one.
            if (schedule.CollectionScheduleId == 0)
            {
                await CreateSchedulesForDaysAsync(SelectedDays, schedule.Status, schedule.Note, schedule.RepeatWeekly, schedule.DumpNumber, SelectedSitioIds, SelectedDriverId, SelectedCollectorIds, SelectedGarbageTruckId);
                TempData["Success"] = "Schedule created successfully.";
                return RedirectToAction("Index");
            }

            // 6) Otherwise, update the existing schedule (and spin off any additional selected days).
            return await UpdateExistingScheduleAsync(schedule, SelectedDays, SelectedSitioIds, SelectedDriverId, SelectedCollectorIds, SelectedGarbageTruckId);
        }

        // Redisplays the Edit form with the selection ViewBag values populated, so the
        // user's picks (sitio/driver/collectors/truck/days) don't disappear when validation fails.
        // Pulled out because this exact block was repeated at every validation
        // failure point in Edit - if the ViewBag keys ever need to change, there's now
        // exactly one place to update instead of three.
        private IActionResult EditFormWithErrors(CollectionSchedule schedule, List<int> SelectedSitioIds, int? SelectedDriverId, List<int> SelectedCollectorIds, int? SelectedGarbageTruckId, List<DayOfWeek> SelectedDays)
        {
            ViewBag.SelectedSitioIds = SelectedSitioIds ?? new List<int>();
            ViewBag.SelectedDriverId = SelectedDriverId;
            ViewBag.SelectedCollectorIds = SelectedCollectorIds ?? new List<int>();
            ViewBag.SelectedGarbageTruckId = SelectedGarbageTruckId;
            ViewBag.SelectedDays = SelectedDays;
            return View(schedule);
        }

        // Checks whether the given person (driver or collector) has a day off that falls
        // on any of the selected collection days. Returns a user-facing message describing
        // the conflict, or null if there's no conflict at all.
        private async Task<string?> FindPersonDaysOffConflictAsync(int? personId, List<DayOfWeek> SelectedDays)
        {
            if (personId == null)
                return null;

            var person = await _context.Collector.FirstOrDefaultAsync(c => c.CollectorId == personId);
            if (person == null || string.IsNullOrWhiteSpace(person.DaysOff))
                return null;

            var daysOff = person.DaysOff.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim()).ToList();
            foreach (var day in SelectedDays)
            {
                if (daysOff.Any(d => string.Equals(d, day.ToString(), StringComparison.OrdinalIgnoreCase)))
                {
                    return $"{person.FirstName} {person.LastName}'s day off is {day}.";
                }
            }

            return null;
        }

        // Runs FindPersonDaysOffConflictAsync for the driver and then every selected
        // collector, stopping at the first conflict found.
        private async Task<string?> FindDaysOffConflictAsync(int? SelectedDriverId, List<int> SelectedCollectorIds, List<DayOfWeek> SelectedDays)
        {
            var driverConflict = await FindPersonDaysOffConflictAsync(SelectedDriverId, SelectedDays);
            if (driverConflict != null)
                return driverConflict;

            foreach (var collectorId in SelectedCollectorIds ?? new List<int>())
            {
                var conflict = await FindPersonDaysOffConflictAsync(collectorId, SelectedDays);
                if (conflict != null)
                    return conflict;
            }

            return null;
        }

        // Adds ModelState errors for any of the four required selections (sitio, truck,
        // driver, at least one collector) that are missing. Doesn't return anything - like
        // the other ModelState checks in this controller, the caller checks ModelState.IsValid afterwards.
        private void ValidateRequiredSelections(List<int> SelectedSitioIds, int? SelectedGarbageTruckId, int? SelectedDriverId, List<int> SelectedCollectorIds)
        {
            if (SelectedSitioIds == null || !SelectedSitioIds.Any())
            {
                ModelState.AddModelError("Sitio", "Please select at least one Sitio.");
            }
            if (SelectedGarbageTruckId == null)
            {
                ModelState.AddModelError("GarbageTruck", "Please select a Garbage Truck.");
            }
            if (SelectedDriverId == null)
            {
                ModelState.AddModelError("Driver", "Please select a driver.");
            }
            if (SelectedCollectorIds == null || !SelectedCollectorIds.Any())
            {
                ModelState.AddModelError("Collector", "Please select at least one collector.");
            }
        }

        // Runs ValidateScheduleAsync (the shared duplicate-sitio and duplicate-dump checks)
        // once per selected day, stopping at the first conflict. Returns that conflict's
        // error message, or null if every selected day is conflict-free.
        private async Task<string?> ValidateSelectedDaysForConflictsAsync(List<DayOfWeek> SelectedDays, string status, int dumpNumber, List<int> SelectedSitioIds, int? SelectedDriverId, int? SelectedGarbageTruckId, int? excludeScheduleId)
        {
            foreach (var day in SelectedDays)
            {
                // Include Status here (not just DayOfWeek) so ValidateScheduleAsync can
                // tell whether THIS schedule is already Completed and skip the
                // sitio-duplicate check accordingly (see the bug-fix comment there).
                // DumpNumber is included too, so the duplicate-dump check below has
                // something to compare against.
                var scheduleForDay = new CollectionSchedule { DayOfWeek = day, Status = status, DumpNumber = dumpNumber };
                var error = await ValidateScheduleAsync(scheduleForDay, SelectedSitioIds, SelectedDriverId, SelectedGarbageTruckId, excludeScheduleId);
                if (error != null)
                    return error;
            }

            return null;
        }

        // Creates one new CollectionSchedule per day in "days", each carrying the same
        // Status/Note/RepeatWeekly/DumpNumber, and links each one to the selected sitios
        // plus the single collector/truck. This is the exact same work that used to be
        // duplicated in two places in Edit (once for brand-new schedules, once for
        // "additional selected days" on an existing schedule) - both call this one method now.
        private async Task CreateSchedulesForDaysAsync(IEnumerable<DayOfWeek> days, string status, string? note, bool repeatWeekly, int dumpNumber, List<int> SelectedSitioIds, int? SelectedDriverId, List<int> SelectedCollectorIds, int? SelectedGarbageTruckId)
        {
            foreach (var day in days)
            {
                // Same per-day resolution as Create - a dump number free on one day may
                // already be taken on another day for the same truck.
                var resolvedDumpNumber = await ResolveDumpNumberAsync(SelectedGarbageTruckId, day, dumpNumber, excludeScheduleId: null);

                var newSchedule = new CollectionSchedule
                {
                    Status = status,
                    Note = note,
                    RepeatWeekly = repeatWeekly,
                    DayOfWeek = day,
                    DumpNumber = resolvedDumpNumber,
                    TruckId = SelectedGarbageTruckId,
                    DriverId = SelectedDriverId,
                    CreatedDate = WeeklyRecurrenceHelper.CalculateScheduledDate(day)
                };
                _context.CollectionSchedule.Add(newSchedule);
                await _context.SaveChangesAsync(); // save first so newSchedule.CollectionScheduleId is populated
                AddScheduleLinks(newSchedule.CollectionScheduleId, SelectedSitioIds);
                AddCollectorLinks(newSchedule.CollectionScheduleId, SelectedCollectorIds);
            }
            await _context.SaveChangesAsync(); // persist all the staged links added above
        }

        // Updates an existing schedule's own fields (including the single driver/truck),
        // replaces its sitio and collector links to match the form, and creates any additional
        // selected days as new schedules (a schedule can only have one DayOfWeek, so 2nd/3rd/etc.
        // selected days become their own records).
        private async Task<IActionResult> UpdateExistingScheduleAsync(CollectionSchedule schedule, List<DayOfWeek> SelectedDays, List<int> SelectedSitioIds, int? SelectedDriverId, List<int> SelectedCollectorIds, int? SelectedGarbageTruckId)
        {
            // Load an untracked ("AsNoTracking") copy of the current database values so we can
            // keep the original CreatedDate/Status without EF trying to track two copies of the same entity.
            var original = await _context.CollectionSchedule
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.CollectionScheduleId == schedule.CollectionScheduleId);

            if (original != null)
            {
                // Keep the original creation date and status (this Edit form doesn't change status),
                // and stamp the update time as now.
                schedule.CreatedDate = original.CreatedDate;
                schedule.Status = original.Status;
                schedule.DateOfCompletion = original.DateOfCompletion;
                schedule.UpdatedDate = DateTime.Now;
            }

            // Set the single truck and single driver directly on the schedule being saved.
            schedule.TruckId = SelectedGarbageTruckId;
            schedule.DriverId = SelectedDriverId;

            // Resolve the Dump Number against the (possibly new) truck/day combo, excluding
            // this schedule itself from the "already taken" check. If the truck or day
            // changed as part of this edit, the previously-valid number might now collide
            // with a different schedule - in which case this bumps it to the next free one.
            schedule.DumpNumber = await ResolveDumpNumberAsync(SelectedGarbageTruckId, schedule.DayOfWeek, schedule.DumpNumber, schedule.CollectionScheduleId);

            // Tell EF Core this entity's values should be treated as changed, so all fields get saved.
            _context.Entry(schedule).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // Sync the sitio and collector many-to-many links to match what's currently selected in the form.
            await ReplaceScheduleLinksAsync(schedule.CollectionScheduleId, SelectedSitioIds);
            await ReplaceCollectorLinksAsync(schedule.CollectionScheduleId, SelectedCollectorIds);

            // Create matching schedules for any additional selected days (the first day is
            // already covered by the update above; SelectedDays.Skip(1) is every day after that).
            await CreateSchedulesForDaysAsync(SelectedDays.Skip(1), schedule.Status, schedule.Note, schedule.RepeatWeekly, schedule.DumpNumber, SelectedSitioIds, SelectedDriverId, SelectedCollectorIds, SelectedGarbageTruckId);

            TempData["Success"] = "Schedule updated successfully.";
            return RedirectToAction("Index");
        }

        // Replaces a schedule's sitio join-table rows: deletes everything currently linked,
        // then re-adds exactly what's selected in the form. This is the simplest reliable
        // way to sync a many-to-many relationship with a multi-select form post, since the
        // form doesn't tell us which specific links were added/removed - just the final
        // selected set. (Truck and Collector don't need this - they're plain fields, updated
        // directly in UpdateExistingScheduleAsync above.)
        private async Task ReplaceScheduleLinksAsync(int scheduleId, List<int> SelectedSitioIds)
        {
            var existingSitios = _context.CollectionScheduleSitio.Where(j => j.CollectionScheduleId == scheduleId);
            _context.CollectionScheduleSitio.RemoveRange(existingSitios);

            await _context.SaveChangesAsync();

            AddScheduleLinks(scheduleId, SelectedSitioIds);
            await _context.SaveChangesAsync();
        }

        // Attaches Sitio join-links for a schedule.
        private void AddScheduleLinks(int scheduleId, IEnumerable<int>? sitioIds)
        {
            foreach (var sitioId in sitioIds?.Distinct() ?? Enumerable.Empty<int>())
            {
                _context.CollectionScheduleSitio.Add(new CollectionScheduleSitio
                {
                    CollectionScheduleId = scheduleId,
                    SitioId = sitioId
                });
            }
        }

        // Replaces a schedule's collector join-table rows: deletes everything currently
        // linked, then re-adds exactly what's selected in the form. Same approach as
        // ReplaceScheduleLinksAsync above, just for Collectors instead of Sitios.
        private async Task ReplaceCollectorLinksAsync(int scheduleId, List<int> SelectedCollectorIds)
        {
            var existingCollectors = _context.CollectionScheduleCollector.Where(j => j.CollectionScheduleId == scheduleId);
            _context.CollectionScheduleCollector.RemoveRange(existingCollectors);

            await _context.SaveChangesAsync();

            AddCollectorLinks(scheduleId, SelectedCollectorIds);
            await _context.SaveChangesAsync();
        }

        // Attaches Collector join-links for a schedule.
        private void AddCollectorLinks(int scheduleId, IEnumerable<int>? collectorIds)
        {
            foreach (var collectorId in collectorIds?.Distinct() ?? Enumerable.Empty<int>())
            {
                _context.CollectionScheduleCollector.Add(new CollectionScheduleCollector
                {
                    CollectionScheduleId = scheduleId,
                    CollectorId = collectorId
                });
            }
        }

        // Shared business-rule validation used by both Create and Edit.
        // Returns an error message if validation fails; otherwise returns null.
        // "excludeScheduleId" lets Edit calls skip comparing the schedule against itself
        // when checking for duplicate sitio assignments.
        private async Task<string> ValidateScheduleAsync(CollectionSchedule schedule, List<int> SelectedSitioIds, int? SelectedDriverId, int? SelectedGarbageTruckId, int? excludeScheduleId)
        {

            // Rule 1: Verify the selected garbage truck exists and is not under maintenance.
            if (SelectedGarbageTruckId != null)
            {
                var truck = await _context.GarbageTruck.FirstOrDefaultAsync(t => t.TruckId == SelectedGarbageTruckId.Value);
                if (truck == null)
                {
                    ModelState.AddModelError("GarbageTruck", "The selected truck was not found.");
                    return "The selected truck was not found.";
                }
                // Block scheduling a truck that's currently marked as under maintenance.
                if (truck.StatusFlag == "Maintenance")
                {
                    ModelState.AddModelError("GarbageTruck", "The selected truck is currently under maintenance.");
                    return "The selected truck is currently under maintenance and cannot be scheduled.";
                }
            }

            // Rule 2: Verify the selected driver actually exists.
            if (SelectedDriverId != null)
            {
                var driverExists = await _context.Collector.AnyAsync(c => c.CollectorId == SelectedDriverId);
                if (!driverExists)
                {
                    ModelState.AddModelError("Driver", "The selected driver was not found.");
                    return "The selected driver was not found.";
                }
            }

            // Completed schedules are historical records and do not reserve their sitios.
            //
            // BUG FIX: this check used to only look at the OTHER schedules being
            // scanned (via "s.Status != Completed" below) - it never checked whether
            // the schedule currently being saved is ITSELF already Completed. That
            // meant editing a Completed schedule (e.g. just to toggle Repeat Weekly)
            // could get blocked by its own auto-generated "next occurrence" record,
            // because that successor record shares the same Sitio and DayOfWeek and
            // is still Pending/active. Since a Completed schedule is already
            // historical and isn't reserving anything going forward, we skip this
            // check completely when schedule.Status == "Completed".
            if (schedule.Status != "Completed" && SelectedSitioIds != null && SelectedSitioIds.Any())
            {
                // Check whether any OTHER schedule (excluding the one being edited, if any)
                // already has one of the currently selected sitios attached to it.
                //
                // ParentScheduleId note: we also exclude any schedule that is a direct
                // CHILD of the record being edited (s.ParentScheduleId == excludeScheduleId).
                // Before ParentScheduleId existed, the only way to know two schedule rows
                // were "the same recurring series" was to notice they happened to share a
                // Sitio and DayOfWeek - which is exactly what caused this bug. Now that the
                // relationship is stored explicitly, we can exclude a schedule's own
                // auto-generated successor directly instead of relying only on Status.
                var duplicate = await _context.CollectionSchedule
                    .Include(s => s.CollectionScheduleSitios)
                    .Where(s => s.CollectionScheduleId != (excludeScheduleId ?? 0) &&
                                s.ParentScheduleId != (excludeScheduleId ?? 0) &&
                                s.Status != "Completed" &&
                                s.DayOfWeek == schedule.DayOfWeek)
                    .AnyAsync(s => s.CollectionScheduleSitios.Any(css => SelectedSitioIds.Contains(css.SitioId)));

                if (duplicate)
                {
                    ModelState.AddModelError("Sitio", "A schedule already exists for one or more selected sitios.");
                    return "A schedule already exists for one or more selected sitios.";
                }
            }

            // Rule 3 (formerly here): duplicate truck+day+DumpNumber combinations used to be
            // BLOCKED with a validation error, forcing the user to manually retype a free
            // dump number. That's been replaced with automatic assignment - see
            // ResolveDumpNumberAsync below, which every schedule-creating/updating code path
            // now calls instead of trusting the DumpNumber typed into the form. Because of
            // that, a duplicate should never actually reach the database anymore, so there's
            // nothing left to validate/block here.

            // All validation rules passed successfully.
            return null;
        }

        // Figures out which Dump Number a schedule should actually be saved with for a given
        // truck + day. Dump Numbers must be unique per truck per day (they exist so the SAME
        // truck can run more than one route on the SAME day - Dump 1 = morning route, Dump 2 =
        // afternoon route, etc) and are meant to be assigned sequentially/densely (1, 2, 3, ...)
        // rather than requiring the user to guess a free number.
        //
        // Behavior: if "requestedDumpNumber" (whatever was typed/carried over on the form) is
        // still free for this truck+day, it's kept as-is - so editing a schedule that already
        // has a valid number doesn't get silently renumbered. Only when it collides with
        // another ACTIVE schedule's dump number does this hand back the smallest number that
        // isn't currently taken, filling any gaps left by deleted/completed schedules instead
        // of always growing upward.
        //
        // "excludeScheduleId" excludes the schedule being edited (and its auto-generated
        // successor) from the "already taken" set, same pattern used elsewhere in this
        // controller. Completed schedules are historical and don't reserve a dump number.
        private async Task<int> ResolveDumpNumberAsync(int? truckId, DayOfWeek? day, int requestedDumpNumber, int? excludeScheduleId)
        {
            // No truck or no day yet means there's nothing to collide with; just keep
            // whatever was requested (other validation already requires these to be set
            // before the schedule can actually be saved).
            if (truckId == null || day == null)
                return requestedDumpNumber;

            var usedDumpNumbers = new HashSet<int>(await _context.CollectionSchedule
                .Where(s => s.CollectionScheduleId != (excludeScheduleId ?? 0) &&
                            s.ParentScheduleId != (excludeScheduleId ?? 0) &&
                            s.Status != "Completed" &&
                            s.DayOfWeek == day &&
                            s.TruckId == truckId)
                .Select(s => s.DumpNumber)
                .ToListAsync());

            if (!usedDumpNumbers.Contains(requestedDumpNumber))
                return requestedDumpNumber;

            // Requested number is already taken - find the smallest free one instead.
            int candidate = 1;
            while (usedDumpNumbers.Contains(candidate))
            {
                candidate++;
            }
            return candidate;
        }

        // POST: Update Status
        // Updates the current status of a collection schedule. Called from the
        // Collection Status dropdown + Save button on CollectionSchedule/Details.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            // Retrieve the selected schedule together with its assigned sitios, since we
            // need to check whether they've all been collected before allowing "Completed".
            var schedule = await _context.CollectionSchedule
                .Include(s => s.CollectionScheduleSitios)
                .FirstOrDefaultAsync(s => s.CollectionScheduleId == id);

            // Return 404 if the schedule cannot be found.
            if (schedule == null)
                return NotFound();

            // List of allowed schedule statuses (matches the dropdown on the Details page).
            var validStatuses = new[] { "Pending", "Delayed", "Completed" };

            // Ensure that only valid status values are accepted.
            if (!validStatuses.Contains(status))
            {
                TempData["Error"] = "Invalid status value.";
                return RedirectToAction("Details", new { id });
            }

            // A schedule can only be marked Completed once every assigned sitio's own
            // Status is "Completed" (set via CollectionMonitoring/Edit). This is a
            // server-side copy of the same rule the Details page dropdown already enforces,
            // so it still holds even if the request didn't come from that page.
            var allSitiosCollected = schedule.CollectionScheduleSitios.Any() &&
                schedule.CollectionScheduleSitios.All(cs => cs.Status == "Completed");

            if (status == "Completed" && !allSitiosCollected)
            {
                TempData["Error"] = "All assigned sitios must be marked as collected before this schedule can be set to Completed.";
                return RedirectToAction("Details", new { id });
            }

            // Once every sitio is already collected, "Delayed" no longer makes sense -
            // same server-side copy of the rule the Details page dropdown enforces.
            if (status == "Delayed" && allSitiosCollected)
            {
                TempData["Error"] = "This schedule cannot be set to Delayed once all assigned sitios are collected.";
                return RedirectToAction("Details", new { id });
            }

            // Update the schedule status and modification date.
            schedule.Status = status;
            schedule.UpdatedDate = DateTime.Now;

            // Save the updated status.
            _context.Entry(schedule).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // Record this status change in the Status Update History log (the same log
            // used by CollectionMonitoring/Edit's Collected/Uncollected entries), so
            // Completed/Delayed changes made from this dropdown show up there too.
            if (status == "Completed" || status == "Delayed")
            {
                _context.MonitoringLog.Add(new CollectionMonitoring
                {
                    CollectionScheduleId = id,
                    Status = status,
                    LogDate = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }

            // If the schedule is a repeating weekly schedule and was set to Completed OR
            // Delayed, create the next occurrence one week later. Delayed is included
            // because once a schedule is Delayed (its remaining sitios typically already
            // carried over via CarryOver), it's done for the week just like a Completed
            // one - the team still needs a schedule for next week.
            if ((status == "Completed" || status == "Delayed") && schedule.RepeatWeekly)
            {
                try
                {
                    // Ensure DateOfCompletion is recorded when a schedule is completed.
                    // (Only for Completed - Delayed schedules don't have a "completion".)
                    if (status == "Completed" && !schedule.DateOfCompletion.HasValue)
                    {
                        schedule.DateOfCompletion = DateTime.Now;
                        schedule.UpdatedDate = DateTime.Now;
                        _context.Entry(schedule).State = EntityState.Modified;
                        await _context.SaveChangesAsync();
                    }

                    // Determine the scheduled occurrence date for this schedule. Prefer
                    // the stored CreatedDate (it represents the intended scheduled occurrence
                    // when we create schedules). Fall back to DateOfCompletion and then to today.
                    DateTime scheduledDate;
                    if (schedule.CreatedDate.HasValue)
                    {
                        // CreatedDate was set to the occurrence date when the schedule was created.
                        scheduledDate = schedule.CreatedDate.Value.Date;
                    }
                    else
                    {
                        var referenceDate = schedule.DateOfCompletion?.Date ?? DateTime.Today;
                        if (schedule.DayOfWeek.HasValue)
                        {
                            var target = (int)schedule.DayOfWeek.Value; // Sunday = 0
                            var refDow = (int)referenceDate.DayOfWeek;
                            var diff = (refDow - target + 7) % 7; // days to go back to reach the scheduled weekday
                            scheduledDate = referenceDate.AddDays(-diff);
                        }
                        else
                        {
                            scheduledDate = referenceDate;
                        }
                    }

                    var nextCreatedDate = scheduledDate.AddDays(7);
                    // Ensure the next occurrence is in the future. If the computed next date
                    // somehow falls on or before today (due to reference date issues),
                    // advance it by whole weeks until it's after today.
                    while (nextCreatedDate <= DateTime.Today)
                    {
                        nextCreatedDate = nextCreatedDate.AddDays(7);
                    }

                    // The parent schedule is already Completed at this point (and therefore
                    // excluded from "already taken" dump numbers), so its own DumpNumber is
                    // normally free again. Still resolve it through the same helper rather
                    // than assuming that's true, in case another active schedule has since
                    // claimed that truck+day+number combination.
                    var nextDumpNumber = await ResolveDumpNumberAsync(schedule.TruckId, schedule.DayOfWeek, schedule.DumpNumber, excludeScheduleId: schedule.CollectionScheduleId);

                    var nextSchedule = new CollectionSchedule
                    {
                        Status = "Pending",
                        DayOfWeek = schedule.DayOfWeek,
                        RepeatWeekly = true,
                        CreatedDate = nextCreatedDate,
                        // Carry over the Truck and Driver directly - Sitios and Collectors are
                        // copied separately below since they're still many-to-many links.
                        TruckId = schedule.TruckId,
                        DriverId = schedule.DriverId,
                        DumpNumber = nextDumpNumber,
                        // Link this new record back to the schedule that created it,
                        // so we no longer have to guess the relationship from
                        // matching Sitio/DayOfWeek alone.
                        ParentScheduleId = schedule.CollectionScheduleId
                    };
                    _context.CollectionSchedule.Add(nextSchedule);
                    await _context.SaveChangesAsync();

                    // Sitios are still many-to-many, so copy those join links as before.
                    var sitios = _context.CollectionScheduleSitio.Where(j => j.CollectionScheduleId == schedule.CollectionScheduleId).Select(j => j.SitioId).ToList();
                    foreach (var sId in sitios.Distinct())
                    {
                        _context.CollectionScheduleSitio.Add(new CollectionScheduleSitio { CollectionScheduleId = nextSchedule.CollectionScheduleId, SitioId = sId });
                    }

                    // Collectors are also many-to-many, so copy those join links too.
                    var collectorIds = _context.CollectionScheduleCollector.Where(j => j.CollectionScheduleId == schedule.CollectionScheduleId).Select(j => j.CollectorId).ToList();
                    foreach (var cId in collectorIds.Distinct())
                    {
                        _context.CollectionScheduleCollector.Add(new CollectionScheduleCollector { CollectionScheduleId = nextSchedule.CollectionScheduleId, CollectorId = cId });
                    }

                    await _context.SaveChangesAsync();
                }
                catch
                {
                    // Swallow exceptions here to avoid blocking the status update; log if logging is available.
                }
            }

            TempData["Success"] = $"Schedule status updated to {status}.";
            return RedirectToAction("Details", new { id });
        }

        // POST: Carry Over
        // Transfers the selected Delayed sitio(s) off this schedule and onto a single
        // destination schedule (another schedule on the same day, or the next day's
        // schedule if none was available - see BuildCarryOverDestinations below). Used
        // from the Carry Over card on CollectionSchedule/Details before the user then
        // marks the original schedule as Delayed.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CarryOver(int id, List<int> SelectedSitioIds, int DestinationScheduleId)
        {
            var schedule = await _context.CollectionSchedule
                .Include(s => s.CollectionScheduleSitios).ThenInclude(css => css.Sitio)
                .FirstOrDefaultAsync(s => s.CollectionScheduleId == id);

            if (schedule == null)
                return NotFound();

            SelectedSitioIds ??= new List<int>();

            // Only sitios that are Delayed AND not already reassigned on THIS schedule
            // can be carried over (once reassigned, they're done - no re-carrying).
            var sitiosToMove = schedule.CollectionScheduleSitios
                .Where(cs => cs.Status == "Delayed" && cs.ReassignedToScheduleId == null && SelectedSitioIds.Contains(cs.CollectionScheduleSitioId))
                .ToList();

            if (!sitiosToMove.Any())
            {
                TempData["Error"] = "Please select at least one delayed sitio to carry over.";
                return RedirectToAction("Details", new { id });
            }

            var destination = await _context.CollectionSchedule
                .Include(s => s.Driver)
                .Include(s => s.GarbageTruck)
                .FirstOrDefaultAsync(s => s.CollectionScheduleId == DestinationScheduleId);
            if (destination == null)
            {
                TempData["Error"] = "Please select a valid destination schedule.";
                return RedirectToAction("Details", new { id });
            }

            // Sitios already assigned to the destination schedule (unique index on
            // CollectionScheduleId+SitioId), so don't try to add a duplicate.
            var destinationSitioIds = _context.CollectionScheduleSitio
                .Where(css => css.CollectionScheduleId == DestinationScheduleId)
                .Select(css => css.SitioId)
                .ToHashSet();

            var movedNames = new List<string>();

            foreach (var cs in sitiosToMove.OrderBy(cs => cs.Sitio?.SitioName))
            {
                if (!destinationSitioIds.Contains(cs.SitioId))
                {
                    _context.CollectionScheduleSitio.Add(new CollectionScheduleSitio
                    {
                        CollectionScheduleId = DestinationScheduleId,
                        SitioId = cs.SitioId
                        // Status defaults to "Pending" - the destination team still
                        // needs to collect it.
                    });
                }

                movedNames.Add(cs.Sitio?.SitioName ?? "Unknown Sitio");

                // Mark the original link as reassigned instead of deleting it. Deleting
                // it would make it invisible to the Repeat Weekly "copy my sitios to
                // next week" step below (UpdateStatus), which would then silently drop
                // this sitio from next week's occurrence - next week's schedule must
                // reflect the ORIGINAL configuration regardless of this week's carry-over.
                cs.ReassignedToScheduleId = DestinationScheduleId;
            }

            // Log a "Reassigned" history entry on the ORIGINAL schedule describing where
            // the sitio(s) went. This goes in the Notes field of the history entry
            // (not Remarks), so it shows in the Notes column on the Status Update History.
            // Uses the destination's actual scheduled date, driver, and truck - the same
            // information (and the same PlateNumber-then-MVFileNumber fallback for the
            // truck) shown in the Transfer To dropdown - so the note reads e.g.
            // "Reassigned to Monday, 03 Aug 2026 (Driver: John Doe, Truck: ABC-1234)."
            var destinationDate = WeeklyRecurrenceHelper.GetDisplayDate(destination);
            var destinationDriverName = destination.Driver != null
                ? $"{destination.Driver.FirstName} {destination.Driver.LastName}"
                : "No Driver";
            var destinationTruckLabel = destination.GarbageTruck != null
                ? (string.IsNullOrEmpty(destination.GarbageTruck.PlateNumber) ? destination.GarbageTruck.MVFileNumber : destination.GarbageTruck.PlateNumber)
                : "No Truck";
            var reassignmentNote = $"Reassigned to {destinationDate:dddd, dd MMM yyyy} (Driver: {destinationDriverName}, Truck: {destinationTruckLabel}).";

            _context.MonitoringLog.Add(new CollectionMonitoring
            {
                CollectionScheduleId = id,
                Status = "Reassigned",
                SitioNames = string.Join(", ", movedNames),
                Notes = reassignmentNote,
                LogDate = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{movedNames.Count} sitio(s) carried over to {destinationDate:dddd, dd MMM yyyy}.";
            return RedirectToAction("Details", new { id });
        }

        // Builds the list of candidate destination schedules for the Carry Over form:
        // other (non-Completed) schedules on the SAME day first; if there are none,
        // falls back to the next day's (non-Completed) schedules instead.
        private async Task<List<CollectionSchedule>> BuildCarryOverDestinationsAsync(CollectionSchedule schedule)
        {
            if (!schedule.DayOfWeek.HasValue)
                return new List<CollectionSchedule>();

            var sameDay = await _context.CollectionSchedule
                .Include(s => s.Driver)
                .Include(s => s.GarbageTruck)
                .Where(s => s.CollectionScheduleId != schedule.CollectionScheduleId &&
                            s.DayOfWeek == schedule.DayOfWeek &&
                            s.Status != "Completed")
                .ToListAsync();

            if (sameDay.Any())
                return sameDay;

            // No same-day team available - fall back to the next day.
            var nextDay = (DayOfWeek)(((int)schedule.DayOfWeek.Value + 1) % 7);

            return await _context.CollectionSchedule
                .Include(s => s.Driver)
                .Include(s => s.GarbageTruck)
                .Where(s => s.CollectionScheduleId != schedule.CollectionScheduleId &&
                            s.DayOfWeek == nextDay &&
                            s.Status != "Completed")
                .ToListAsync();
        }

        // GET: Delete (Hard Delete)
        // Displays the confirmation page before deleting a schedule.
        public async Task<IActionResult> Delete(int id)
        {
            // Retrieve the selected schedule with its related information.
            var schedule = await _context.CollectionSchedule
                .Include(s => s.CollectionScheduleSitios).ThenInclude(css => css.Sitio)
                .Include(s => s.Driver)
                .Include(s => s.CollectionScheduleCollectors).ThenInclude(csc => csc.Collector)
                .Include(s => s.GarbageTruck)
                .FirstOrDefaultAsync(s => s.CollectionScheduleId == id);

            // Return 404 if the schedule does not exist.
            if (schedule == null)
                return NotFound();

            // Show the confirmation view with the schedule's details.
            return View(schedule);
        }

        // POST: Delete (Hard Delete)
        // Permanently removes the selected schedule from the database.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Find the schedule to be deleted.
            var schedule = await _context.CollectionSchedule.FindAsync(id);

            if (schedule != null)
            {
                // ParentScheduleId uses DeleteBehavior.Restrict (NO ACTION) at the
                // database level, because SQL Server does not allow automatic
                // CASCADE/SET NULL on a self-referencing foreign key. That means if
                // this schedule has any children (auto-generated next occurrences
                // pointing at it via ParentScheduleId) and we try to delete it as-is,
                // the database will reject the delete with a FK constraint error.
                //
                // To keep the intended behavior - deleting an old/completed schedule
                // should never delete or break its active successor - we manually
                // clear ParentScheduleId on any children here, in application code,
                // before removing the parent record.
                var childSchedules = await _context.CollectionSchedule
                    .Where(s => s.ParentScheduleId == id)
                    .ToListAsync();

                foreach (var child in childSchedules)
                {
                    child.ParentScheduleId = null;
                }

                // Remove the schedule and related join records (cascade configured)
                _context.CollectionSchedule.Remove(schedule);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Schedule deleted successfully.";
            }
            // If the schedule was already gone, silently redirect without an error.

            return RedirectToAction("Index");
        }

        // GET: Details
        // Displays complete information about a selected collection schedule.
        public async Task<IActionResult> Details(int id)
        {
            // Try to retrieve by schedule id
            var schedule = await _context.CollectionSchedule
                .Include(s => s.CollectionScheduleSitios).ThenInclude(css => css.Sitio)
                .Include(s => s.Driver)
                .Include(s => s.CollectionScheduleCollectors).ThenInclude(csc => csc.Collector)
                .Include(s => s.GarbageTruck)
                .FirstOrDefaultAsync(s => s.CollectionScheduleId == id);

            if (schedule == null)
            {
                return NotFound();
            }

            // Retrieve every monitoring log entry for this schedule (most recent first),
            // so the Details view can show a full history of status updates and remarks.
            var monitoringHistory = await _context.MonitoringLog
                .Where(m => m.CollectionScheduleId == schedule.CollectionScheduleId)
                .OrderByDescending(m => m.LogDate)
                .ThenByDescending(m => m.CollectionMonitoringId)
                .ToListAsync();

            ViewBag.MonitoringHistory = monitoringHistory;

            // The most recent remark is just the first entry of the history above.
            ViewBag.LatestRemark = monitoringHistory.FirstOrDefault()?.Remarks;

            // If this schedule repeats weekly, gather all related repeating schedules
            // that were created together (same CreatedDate date) and collect their weekdays
            // so the Details view can show "Every Monday, Wednesday" when appropriate.
            if (schedule.RepeatWeekly && schedule.CreatedDate.HasValue)
            {
                var createdDate = schedule.CreatedDate.Value.Date;
                // Find other schedules that are marked RepeatWeekly and share the same creation date.
                var relatedDays = await _context.CollectionSchedule
                    .Where(s => s.RepeatWeekly && s.CreatedDate.HasValue && s.CreatedDate.Value.Date == createdDate)
                    .Select(s => s.DayOfWeek)
                    .Distinct()
                    .ToListAsync();

                // Convert to readable string like "Monday, Wednesday"
                var dayNames = relatedDays.Where(d => d.HasValue).Select(d => d.Value.ToString()).ToList();
                if (dayNames.Any())
                {
                    ViewBag.RepeatDays = string.Join(", ", dayNames);
                }
                else
                {
                    ViewBag.RepeatDays = schedule.DayOfWeek?.ToString();
                }
            }
            else
            {
                ViewBag.RepeatDays = null;
            }

            // Carry Over data: which sitios on this schedule are Delayed, and which
            // other schedules they could be transferred to (same day first, next day
            // as a fallback - see BuildCarryOverDestinationsAsync). Only needed once
            // there's actually something delayed to carry over.
            var delayedSitios = schedule.CollectionScheduleSitios
                .Where(cs => cs.Status == "Delayed" && cs.ReassignedToScheduleId == null)
                .OrderBy(cs => cs.Sitio?.SitioName)
                .ToList();
            ViewBag.DelayedSitios = delayedSitios;

            ViewBag.CarryOverDestinations = delayedSitios.Any()
                ? await BuildCarryOverDestinationsAsync(schedule)
                : new List<CollectionSchedule>();

            return View(schedule);
        }

        // Returns schedules within a specified date range.
        // Used by calendar or dashboard components.
        public async Task<IActionResult> GetSchedulesByDate(DateTime? fromDate, DateTime? toDate)
        {
            // Load schedules with related data.
            var query = _context.CollectionSchedule
                .Include(s => s.CollectionScheduleSitios).ThenInclude(css => css.Sitio)
                .Include(s => s.Driver)
                .Include(s => s.CollectionScheduleCollectors).ThenInclude(csc => csc.Collector)
                .Include(s => s.GarbageTruck)
                .AsQueryable();

            var schedules = await query
                .OrderBy(s => s.Status)
                .ThenByDescending(s => s.CreatedDate)
                .ToListAsync();

            // Apply the date-range filter using each schedule's actual occurrence date
            // (CreatedDate when set, otherwise the next occurrence of its DayOfWeek).
            // This is computed in C# via WeeklyRecurrenceHelper rather than in the SQL query
            // because it isn't a simple column comparison - so we filter in memory after
            // loading. Only bounds that were actually supplied are applied; omitting both
            // keeps the previous "return everything" behavior for existing callers.
            if (fromDate.HasValue || toDate.HasValue)
            {
                var from = fromDate?.Date;
                var to = toDate?.Date;

                schedules = schedules
                    .Where(s =>
                    {
                        var occurrence = WeeklyRecurrenceHelper.GetNextOccurrenceDate(s);
                        if (from.HasValue && occurrence < from.Value)
                            return false;
                        if (to.HasValue && occurrence > to.Value)
                            return false;
                        return true;
                    })
                    .ToList();
            }

            // Return the results as JSON for calendar/dashboard components to consume.
            return Json(schedules);
        }

        // Called from the Create/Edit pages (via JavaScript) whenever the user changes the
        // selected Garbage Truck or Day. The Dump Number field is now read-only and just
        // displays whatever this returns, instead of letting the user type a number.
        // "excludeScheduleId" is passed by the Edit page so a schedule doesn't conflict
        // with its own current Dump Number.
        [HttpGet]
        public async Task<IActionResult> GetNextDumpNumber(int? truckId, DayOfWeek? day, int? excludeScheduleId)
        {
            // Passing 1 as the "requested" number just means: start looking from 1 and
            // return the smallest Dump Number that isn't already taken for this truck/day.
            var nextDumpNumber = await ResolveDumpNumberAsync(truckId, day, 1, excludeScheduleId);
            return Json(new { dumpNumber = nextDumpNumber });
        }

        // Builds the dropdown lists used by the Create and Edit views.
        // Populates Sitio, Collector, Garbage Truck, and Status options.
        // "excludeScheduleId" is the schedule currently being edited (if any), so its own
        // Sitio/Collector assignments don't count as "already booked" against itself.
        private void LoadDropdowns(string selectedStatus = null, int? excludeScheduleId = null)
        {
            // Populate the Sitio list (for multi-select)
            ViewBag.Sitios = new SelectList(
                _context.Sitio.OrderBy(s => s.SitioName),
                "SitioId",
                "SitioName"
            );

            // Populate the Driver list (single-select) - only people whose Role is "Driver".
            ViewBag.DriversList = _context.Collector
                    .Where(c => c.Role == "Driver")
                    .OrderBy(c => c.FirstName)
                    .ThenBy(c => c.LastName)
                    .Select(c => new SelectListItem { Value = c.CollectorId.ToString(), Text = c.FirstName + " " + c.LastName })
                    .ToList();

            // Populate the Collector list for multi-selection (checkboxes) - only people
            // whose Role is "Collector".
            ViewBag.CollectorsList = _context.Collector
                    .Where(c => c.Role == "Collector")
                    .OrderBy(c => c.FirstName)
                    .ThenBy(c => c.LastName)
                    .Select(c => new SelectListItem { Value = c.CollectorId.ToString(), Text = c.FirstName + " " + c.LastName })
                    .ToList();

            // Build a "which day(s) is this person's day off" map, so the Create/Edit view
            // can hide a Driver/Collector from the list once the user picks a day that's
            // one of their days off. Covers everyone in the Collector table regardless of
            // Role, since it's used for both the Driver dropdown and Collector checkboxes.
            var collectorDaysOff = new Dictionary<int, List<string>>();
            var collectorsWithDaysOff = _context.Collector
                .Where(c => c.DaysOff != null && c.DaysOff != "")
                .ToList();
            foreach (var c in collectorsWithDaysOff)
            {
                // DaysOff stored as comma-separated names like "Monday,Wednesday"
                collectorDaysOff[c.CollectorId] = c.DaysOff
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(d => d.Trim())
                    .ToList();
            }
            ViewBag.CollectorDaysOff = collectorDaysOff;

            // Populate the Garbage Truck list for multi-selection (checkboxes)
            // Only trucks that are "Active" or "Maintenance" are shown; the display text prefers
            // the plate number, falling back to the MV file number if no plate is set.
            ViewBag.GarbageTrucksList = _context.GarbageTruck
                    .Where(t => new[] { "Active", "Maintenance" }.Contains(t.StatusFlag))
                    .OrderBy(t => t.PlateNumber)
                    .Select(t => new SelectListItem { Value = t.TruckId.ToString(), Text = (string.IsNullOrEmpty(t.PlateNumber) ? t.MVFileNumber ?? "No Plate/MV" : t.PlateNumber + (string.IsNullOrEmpty(t.MVFileNumber) ? "" : $" ({t.MVFileNumber})")) })
                    .ToList();

            // Build a "which day(s) is this Sitio/Driver/Collector already booked on" map, so
            // the Create/Edit view can hide a Sitio from the checkbox list, or a Driver/Collector
            // from their list, once the user picks a day they're already assigned to on
            // another active schedule. Same exclusions as the duplicate-sitio check in
            // ValidateScheduleAsync: skip Completed schedules (they no longer reserve
            // anything) and skip the schedule being edited (and its auto-generated
            // successor) so it doesn't hide itself.
            var activeSchedules = _context.CollectionSchedule
                .Include(s => s.CollectionScheduleSitios)
                .Include(s => s.CollectionScheduleCollectors)
                .Where(s => s.Status != "Completed" &&
                            s.DayOfWeek != null &&
                            s.CollectionScheduleId != (excludeScheduleId ?? 0) &&
                            s.ParentScheduleId != (excludeScheduleId ?? 0))
                .ToList();

            var sitioDayBookings = new Dictionary<int, List<string>>();
            var collectorDayBookings = new Dictionary<int, List<string>>();

            foreach (var s in activeSchedules)
            {
                var dayName = s.DayOfWeek.ToString();

                foreach (var css in s.CollectionScheduleSitios)
                {
                    if (!sitioDayBookings.ContainsKey(css.SitioId))
                        sitioDayBookings[css.SitioId] = new List<string>();
                    if (!sitioDayBookings[css.SitioId].Contains(dayName))
                        sitioDayBookings[css.SitioId].Add(dayName);
                }

                // The driver counts as "booked" on this day...
                if (s.DriverId.HasValue)
                {
                    if (!collectorDayBookings.ContainsKey(s.DriverId.Value))
                        collectorDayBookings[s.DriverId.Value] = new List<string>();
                    if (!collectorDayBookings[s.DriverId.Value].Contains(dayName))
                        collectorDayBookings[s.DriverId.Value].Add(dayName);
                }

                // ...and so does every collector assigned to this schedule.
                foreach (var csc in s.CollectionScheduleCollectors)
                {
                    if (!collectorDayBookings.ContainsKey(csc.CollectorId))
                        collectorDayBookings[csc.CollectorId] = new List<string>();
                    if (!collectorDayBookings[csc.CollectorId].Contains(dayName))
                        collectorDayBookings[csc.CollectorId].Add(dayName);
                }
            }

            ViewBag.SitioDayBookings = sitioDayBookings;
            ViewBag.CollectorDayBookings = collectorDayBookings;

            // Build the status dropdown and pre-select the provided value (if any).
            ViewBag.StatusOptions = new SelectList(
                new List<SelectListItem>
                {
                new SelectListItem { Value = "All", Text = "All Status" },
                    new SelectListItem { Value = "Pending", Text = "Pending" },
                    new SelectListItem { Value = "Completed", Text = "Completed" },
                    new SelectListItem { Value = "Delayed", Text = "Delayed" }
                },
                "Value",
                "Text",
                selectedStatus ?? "All"
            );
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
        // GET: CollectionSchedule/PublicIndex
        // Public read-only view for residents - no CRUD operations
        [AllowAnonymous]
        public async Task<IActionResult> PublicIndex(string search, string dayFilter = null, int page = 1, DateTime? startDate = null, DateTime? endDate = null)
        {
            var schedules = _context.CollectionSchedule
                .Include(s => s.CollectionScheduleSitios).ThenInclude(css => css.Sitio)
                .Include(s => s.Driver)
                .Include(s => s.CollectionScheduleCollectors).ThenInclude(csc => csc.Collector)
                .Include(s => s.GarbageTruck)
                .AsQueryable();

            // Filter by day
            if (!string.IsNullOrEmpty(dayFilter) && Enum.TryParse<DayOfWeek>(dayFilter, out var parsedDay))
            {
                schedules = schedules.Where(s => s.DayOfWeek == parsedDay);
            }

            // Search filter
            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                schedules = schedules.Where(s =>
                    (s.CollectionScheduleSitios.Any(css => EF.Functions.Like(css.Sitio.SitioName, $"%{search}%"))) ||
                    (s.Driver != null && (EF.Functions.Like(s.Driver.FirstName, $"%{search}%") ||
                                          EF.Functions.Like(s.Driver.LastName, $"%{search}%") ||
                                          EF.Functions.Like(s.Driver.FirstName + " " + s.Driver.LastName, $"%{search}%"))) ||
                    (s.CollectionScheduleCollectors.Any(csc =>
                        EF.Functions.Like(csc.Collector.FirstName, $"%{search}%") ||
                        EF.Functions.Like(csc.Collector.LastName, $"%{search}%") ||
                        EF.Functions.Like(csc.Collector.FirstName + " " + csc.Collector.LastName, $"%{search}%"))) ||
                    (s.GarbageTruck != null && EF.Functions.Like(s.GarbageTruck.PlateNumber, $"%{search}%")) ||
                    (s.GarbageTruck != null && EF.Functions.Like(s.GarbageTruck.MVFileNumber, $"%{search}%")) ||
                    (s.Status != null && EF.Functions.Like(s.Status, $"%{search}%"))
                );
            }

            ViewBag.Search = search;
            ViewBag.DayFilter = dayFilter;

            var scheduleList = await schedules.ToListAsync();

            // Sort by next occurrence date
            var ordered = scheduleList
                .OrderBy(s => WeeklyRecurrenceHelper.GetNextOccurrenceDate(s))
                .ThenBy(s => WeeklyRecurrenceHelper.GetStatusPriority(s.Status))
                .ThenByDescending(s => s.CreatedDate)
                .ToList();

            // Date range filter
            if (startDate.HasValue || endDate.HasValue)
            {
                if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
                {
                    (startDate, endDate) = (endDate, startDate);
                }

                ordered = ordered.Where(s =>
                {
                    var displayDate = WeeklyRecurrenceHelper.GetDisplayDate(s).Date;
                    if (startDate.HasValue && displayDate < startDate.Value.Date) return false;
                    if (endDate.HasValue && displayDate > endDate.Value.Date) return false;
                    return true;
                }).ToList();
            }

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            bool isDateRangeMode = startDate.HasValue || endDate.HasValue;
            ViewBag.IsDateRangeMode = isDateRangeMode;

            List<CollectionSchedule> pagedSchedules;

            if (isDateRangeMode)
            {
                pagedSchedules = ordered;
                ViewBag.CurrentPage = 1;
                ViewBag.TotalPages = 1;
            }
            else
            {
                var currentWeekStart = GetWeekStart(DateTime.Today);
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
                if (totalPages < 1) totalPages = 1;

                if (page < 1) page = 1;
                if (page > totalPages) page = totalPages;

                var pageWeekStart = currentWeekStart.AddDays((page - 1) * 7);
                var pageWeekEnd = pageWeekStart.AddDays(6);

                pagedSchedules = ordered
                    .Where(s => GetWeekStart(WeeklyRecurrenceHelper.GetDisplayDate(s)) == pageWeekStart)
                    .ToList();

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.WeekStart = pageWeekStart;
                ViewBag.WeekEnd = pageWeekEnd;
            }

            return View(pagedSchedules);
        }
    }
}