using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TisaWasteManagement.Data;
using TisaWasteManagement.Models;
using TisaWasteManagement.Helpers;

[RequireStaffRole("Admin")]
public class GarbageTruckController : Controller
{
    private readonly ApplicationDbContext _context;

    public GarbageTruckController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /GarbageTruck/Index
    public async Task<IActionResult> Index()
    {
        // Default: Show all records (no soft delete)
        return View(await _context.GarbageTruck.ToListAsync());
    }

    // GET: /GarbageTruck/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: /GarbageTruck/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("TruckId,PlateNumber,MVFileNumber,StatusFlag")] GarbageTruck garbagetruck)
    {
        // Check which fields have values
        bool hasPlate = !string.IsNullOrWhiteSpace(garbagetruck.PlateNumber);
        bool hasMv = !string.IsNullOrWhiteSpace(garbagetruck.MVFileNumber);

        // Business rule: At least one identifier required
        if (!hasPlate && !hasMv)
        {
            ModelState.AddModelError(string.Empty, "Please enter a Plate Number or MV File Number.");
        }

        // Clear validation for empty fields when the other is provided
        if (hasPlate && !hasMv) ModelState.Remove("MVFileNumber");
        if (hasMv && !hasPlate) ModelState.Remove("PlateNumber");

        // Status is required
        if (string.IsNullOrWhiteSpace(garbagetruck.StatusFlag))
        {
            ModelState.Remove("StatusFlag");
            ModelState.AddModelError("StatusFlag", "Please select a status.");
        }

        // Duplicate check for PlateNumber (only if provided)
        if (hasPlate && _context.GarbageTruck.Any(g => g.PlateNumber == garbagetruck.PlateNumber))
        {
            ModelState.AddModelError("PlateNumber", "This number is already registered.");
        }

        // Duplicate check for MVFileNumber (only if provided)
        if (hasMv)
        {
            // Validate MV format: exactly 15 digits
            if (garbagetruck.MVFileNumber!.Length != 15 || !garbagetruck.MVFileNumber.All(char.IsDigit))
            {
                ModelState.AddModelError("MVFileNumber", "MV number must be exactly 15 digits.");
            }

            // Check if MV number is already registered
            if (_context.GarbageTruck.Any(g => g.MVFileNumber == garbagetruck.MVFileNumber))
            {
                ModelState.AddModelError("MVFileNumber", "This number is already registered.");
            }
        }

        if (ModelState.IsValid)
        {
            // IMPORTANT FIX: Set null for empty values to work with filtered unique indexes
            garbagetruck.PlateNumber = string.IsNullOrWhiteSpace(garbagetruck.PlateNumber) ? null : garbagetruck.PlateNumber;
            garbagetruck.MVFileNumber = string.IsNullOrWhiteSpace(garbagetruck.MVFileNumber) ? null : garbagetruck.MVFileNumber;

            _context.Add(garbagetruck);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Truck created successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View(garbagetruck);
    }

    // GET: /GarbageTruck/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var garbagetruck = await _context.GarbageTruck.FindAsync(id);
        if (garbagetruck == null) return NotFound();

        return View(garbagetruck);
    }

    // POST: /GarbageTruck/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("TruckId,PlateNumber,MVFileNumber,StatusFlag")] GarbageTruck garbagetruck)
    {
        if (id != garbagetruck.TruckId) return NotFound();

        bool hasPlate = !string.IsNullOrWhiteSpace(garbagetruck.PlateNumber);
        bool hasMv = !string.IsNullOrWhiteSpace(garbagetruck.MVFileNumber);

        // At least one identifier required
        if (!hasPlate && !hasMv)
        {
            ModelState.AddModelError(string.Empty, "Please enter a Plate Number or MV File Number.");
        }

        // Clear validation for empty fields
        if (hasPlate && !hasMv) ModelState.Remove("MVFileNumber");
        if (hasMv && !hasPlate) ModelState.Remove("PlateNumber");

        // Status is required
        if (string.IsNullOrWhiteSpace(garbagetruck.StatusFlag))
        {
            ModelState.Remove("StatusFlag");
            ModelState.AddModelError("StatusFlag", "Please select a status.");
        }

        // Duplicate check for PlateNumber (excluding current record)
        if (hasPlate && _context.GarbageTruck.Any(g => g.PlateNumber == garbagetruck.PlateNumber && g.TruckId != garbagetruck.TruckId))
        {
            ModelState.AddModelError("PlateNumber", "This number is already registered.");
        }

        // Duplicate check for MVFileNumber (excluding current record)
        if (hasMv)
        {
            if (garbagetruck.MVFileNumber!.Length != 15 || !garbagetruck.MVFileNumber.All(char.IsDigit))
            {
                ModelState.AddModelError("MVFileNumber", "MV number must be exactly 15 digits.");
            }

            if (_context.GarbageTruck.Any(g => g.MVFileNumber == garbagetruck.MVFileNumber && g.TruckId != garbagetruck.TruckId))
            {
                ModelState.AddModelError("MVFileNumber", "This number is already registered.");
            }
        }

        if (ModelState.IsValid)
        {
            try
            {
                // IMPORTANT FIX: Set null for empty values to work with filtered unique indexes
                garbagetruck.PlateNumber = string.IsNullOrWhiteSpace(garbagetruck.PlateNumber) ? null : garbagetruck.PlateNumber;
                garbagetruck.MVFileNumber = string.IsNullOrWhiteSpace(garbagetruck.MVFileNumber) ? null : garbagetruck.MVFileNumber;

                _context.Update(garbagetruck);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Truck updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!GarbageTruckExists(garbagetruck.TruckId))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(garbagetruck);
    }

    // GET: /GarbageTruck/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var garbagetruck = await _context.GarbageTruck
            .FirstOrDefaultAsync(m => m.TruckId == id);
        if (garbagetruck == null) return NotFound();

        return View(garbagetruck);
    }

    // POST: /GarbageTruck/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var garbagetruck = await _context.GarbageTruck.FindAsync(id);
        if (garbagetruck != null)
        {
            _context.GarbageTruck.Remove(garbagetruck);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Truck permanently deleted successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    // Helper method
    private bool GarbageTruckExists(int? id)
    {
        return _context.GarbageTruck.Any(e => e.TruckId == id);
    }
}