using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Models;
using TisaWasteManagement.Data;
using TisaWasteManagement.Helpers;

// Controller responsible for managing collector records.
[RequireStaffRole("Admin")]
public class CollectorController : Controller
{
    // Database context used to access collector data.
    private readonly ApplicationDbContext _context;

    // Constructor that injects the application's database context.
    public CollectorController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Collector/Index
    // Displays the list of all collectors.
    public async Task<IActionResult> Index()
    {
        // Default: Show all records
        return View(await _context.Collector.ToListAsync());
    }

    // GET: /Collector/Create
    // Displays the form for creating a new collector.
    public IActionResult Create()
    {
        return View();
    }

    // POST: /Collector/Create
    // Saves a newly created collector record.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("CollectorId,FirstName,LastName,ContactNumber,Address,Role")] Collector collector, string[] SelectedDaysOff)
    {
        // soft-delete removed: no IsActive flag to set

        // Store the selected days off as a comma-separated string.
        collector.DaysOff = SelectedDaysOff != null && SelectedDaysOff.Length > 0
            ? string.Join(",", SelectedDaysOff)
            : null;

        // Check whether the contact number already exists.
        if (!string.IsNullOrWhiteSpace(collector.ContactNumber) &&
            _context.Collector.Any(c => c.ContactNumber == collector.ContactNumber))
        {
            ModelState.AddModelError("ContactNumber", "Phone number already exists.");
        }

        // Validate that Role is one of the allowed values.
        if (collector.Role != "Collector" && collector.Role != "Driver")
        {
            ModelState.AddModelError("Role", "Please select a valid role.");
        }

        // Validate that all required fields have values.
        if (string.IsNullOrWhiteSpace(collector.FirstName) ||
            string.IsNullOrWhiteSpace(collector.LastName) ||
            string.IsNullOrWhiteSpace(collector.ContactNumber) ||
            string.IsNullOrWhiteSpace(collector.Address))
        {
            ModelState.AddModelError(string.Empty, "Please complete all required fields.");
        }

        // Save the collector if all validations pass.
        if (ModelState.IsValid)
        {
            _context.Add(collector);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Collector created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // Redisplay the form if validation fails.
        return View(collector);
    }

    // GET: /Collector/Edit/5
    // Displays the edit form for the selected collector.
    public async Task<IActionResult> Edit(int? id)
    {
        // Ensure a valid collector ID is provided.
        if (id == null)
            return NotFound();

        // Retrieve the collector record.
        var collector = await _context.Collector.FindAsync(id);

        // Return 404 if the collector does not exist.
        if (collector == null)
            return NotFound();

        return View(collector);
    }

    // POST: /Collector/Edit/5
    // Updates an existing collector record.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("CollectorId,FirstName,LastName,ContactNumber,Address,Role")] Collector collector, string[] SelectedDaysOff)
    {
        // Ensure the submitted record matches the requested collector.
        if (id != collector.CollectorId)
            return NotFound();

        // Store the selected days off as a comma-separated string.
        collector.DaysOff = SelectedDaysOff != null && SelectedDaysOff.Length > 0
            ? string.Join(",", SelectedDaysOff)
            : null;

        // Check for duplicate contact numbers, excluding the current collector.
        if (!string.IsNullOrWhiteSpace(collector.ContactNumber) &&
            _context.Collector.Any(c => c.ContactNumber == collector.ContactNumber && c.CollectorId != collector.CollectorId))
        {
            ModelState.AddModelError("ContactNumber", "Phone number already exists.");
        }

        // Validate that Role is one of the allowed values.
        if (collector.Role != "Collector" && collector.Role != "Driver")
        {
            ModelState.AddModelError("Role", "Please select a valid role.");
        }

        // Validate required fields.
        if (string.IsNullOrWhiteSpace(collector.FirstName) ||
            string.IsNullOrWhiteSpace(collector.LastName) ||
            string.IsNullOrWhiteSpace(collector.ContactNumber) ||
            string.IsNullOrWhiteSpace(collector.Address))
        {
            ModelState.AddModelError(string.Empty, "Please complete all required fields.");
        }

        // Save changes if validation succeeds.
        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(collector);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Collector updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                // Verify that the collector still exists before rethrowing the exception.
                if (!CollectorExists(collector.CollectorId))
                    return NotFound();
                else
                    throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // Redisplay the form if validation fails.
        return View(collector);
    }

    // GET: /Collector/Delete/5
    // Displays the delete confirmation page.
    public async Task<IActionResult> Delete(int? id)
    {
        // Ensure a valid collector ID is provided.
        if (id == null)
            return NotFound();

        // Retrieve the selected collector.
        var collector = await _context.Collector
            .FirstOrDefaultAsync(m => m.CollectorId == id);

        // Return 404 if the collector does not exist.
        if (collector == null)
            return NotFound();

        return View(collector);
    }

    // POST: /Collector/Delete/5
    // Permanently removes the selected collector.
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        // Retrieve the collector to delete.
        var collector = await _context.Collector.FindAsync(id);

        if (collector != null)
        {
            // Remove the collector record from the database.
            _context.Collector.Remove(collector);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Collector permanently deleted successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    // GET: /Collector/Details/5
    // Displays detailed information about a collector.
    public async Task<IActionResult> Details(int? id)
    {
        // Ensure a valid collector ID is provided.
        if (id == null)
            return NotFound();

        // Retrieve the selected collector.
        var collector = await _context.Collector
            .FirstOrDefaultAsync(m => m.CollectorId == id);

        // Return 404 if the collector does not exist.
        if (collector == null)
            return NotFound();

        return View(collector);
    }

    // Helper method used to determine whether a collector exists.
    private bool CollectorExists(int? id)
    {
        return _context.Collector.Any(e => e.CollectorId == id);
    }
}