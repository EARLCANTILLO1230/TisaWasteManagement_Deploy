using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Models;
using TisaWasteManagement.Data;
using TisaWasteManagement.Helpers;

[RequireStaffRole("Admin")]
public class SitioController : Controller
{
    private readonly ApplicationDbContext _context;

    public SitioController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Sitio/Index
    // Modified to accept a page number parameter for pagination
    public async Task<IActionResult> Index(int? pageNumber)
    {
        // Set the number of records to display per page
        int pageSize = 5; // You can change this number to show more or fewer records per page

        // Get all records from the database and sort them alphabetically by SitioName
        // OrderBy is used to sort the list in ascending alphabetical order (A to Z)
        // This makes it easier for users to find specific sitios
        var sitios = _context.Sitio.OrderBy(s => s.SitioName).AsQueryable();

        // Apply pagination using the PaginatedList helper class
        // This will get only the records needed for the current page
        var paginatedSitios = await PaginatedList<Sitio>.CreateAsync(sitios, pageNumber ?? 1, pageSize);

        // Pass the paginated list to the view
        // The view will display only the records for the current page
        return View(paginatedSitios);
    }

    // GET: /Sitio/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: /Sitio/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("SitioId,SitioName")] Sitio sitio)
    {
        // Check if the sitio name already exists (case-insensitive)
        var normalizedName = NormalizeSitioName(sitio.SitioName);
        if (SitioNameExists(normalizedName))
        {
            ModelState.AddModelError("SitioName", "Sitio name already exists. Please enter a unique name.");
        }

        if (ModelState.IsValid)
        {
            _context.Add(sitio);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Sitio created successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View(sitio);
    }

    // GET: /Sitio/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var sitio = await _context.Sitio.FindAsync(id);
        if (sitio == null) return NotFound();

        return View(sitio);
    }

    // POST: /Sitio/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("SitioId,SitioName")] Sitio sitio)
    {
        if (id != sitio.SitioId) return NotFound();

        // Check duplicate name (excluding current record)
        var normalized = NormalizeSitioName(sitio.SitioName);
        if (SitioNameExists(normalized, excludeId: sitio.SitioId))
        {
            ModelState.AddModelError("SitioName", "Sitio name already exists. Please enter a unique name.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(sitio);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Sitio updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SitioExists(sitio.SitioId))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }

        return View(sitio);
    }

    // GET: /Sitio/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var sitio = await _context.Sitio
            .FirstOrDefaultAsync(m => m.SitioId == id);
        if (sitio == null) return NotFound();

        return View(sitio);
    }

    // POST: /Sitio/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var sitio = await _context.Sitio.FindAsync(id);
        if (sitio != null)
        {
            _context.Sitio.Remove(sitio);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Sitio permanently deleted successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ============================================
    // HELPER METHODS
    // ============================================

    // Checks if a sitio exists by ID
    private bool SitioExists(int? id)
    {
        return _context.Sitio.Any(e => e.SitioId == id);
    }

    // Normalizes a sitio name for case-insensitive comparison
    private string NormalizeSitioName(string? name)
    {
        return (name ?? string.Empty).Trim().ToLowerInvariant();
    }

    // Checks if a normalized sitio name already exists in the database
    private bool SitioNameExists(string normalizedName, int? excludeId = null)
    {
        if (excludeId.HasValue)
        {
            // When editing, exclude the current record
            return _context.Sitio.Any(s => s.SitioId != excludeId.Value &&
                                           ((s.SitioName ?? "").Trim().ToLower()) == normalizedName);
        }
        // When creating, check all records
        return _context.Sitio.Any(s => ((s.SitioName ?? "").Trim().ToLower()) == normalizedName);
    }
}