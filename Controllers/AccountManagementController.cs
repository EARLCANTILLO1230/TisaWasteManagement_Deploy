// Controllers/AccountManagementController.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TisaWasteManagement.Data;
using TisaWasteManagement.Helpers;
using TisaWasteManagement.Models;

namespace TisaWasteManagement.Controllers
{
    // Combined "Account Management" module.
    //
    // Lets a logged-in Admin manage BOTH Admin and Inspector staff login
    // accounts from one place: view a single combined list, create new
    // Inspector accounts, rename existing accounts, and reset passwords.
    //
    // NOTE: Creating new Admin accounts is NOT supported here - Create only
    // ever makes Inspector accounts (see the Create action below). Existing
    // Admin accounts can still be listed, edited, and have their password
    // reset like any other account.
    //
    // This controller REPLACES the two controllers that used to exist
    // (AdminAccountController and InspectorAccountController). The
    // underlying database tables are UNCHANGED - AdminAccount and
    // InspectorAccount are still two separate tables (see AdminAccount.cs /
    // InspectorAccount.cs). This controller just looks at the "type" the
    // page sends (Admin or Inspector) and talks to whichever table matches.
    //
    // Routes look like:
    //   GET  /AccountManagement                        -> Index
    //   GET  /AccountManagement/Create                  -> Create (blank form, always Inspector)
    //   POST /AccountManagement/Create                  -> Create (save)
    //   GET  /AccountManagement/Edit/Admin/5             -> Edit (form)
    //   POST /AccountManagement/Edit/Admin/5             -> Edit (save)
    //   GET  /AccountManagement/ChangePassword/Inspector/5
    //   POST /AccountManagement/ChangePassword/Inspector/5
    [RequireStaffRole("Admin")]
    [Route("AccountManagement")]
    public class AccountManagementController : Controller
    {
        private readonly ApplicationDbContext _context;

        // One PasswordHasher per account type - same tool as before, just
        // both living in this single controller now instead of two.
        private readonly PasswordHasher<AdminAccount> _adminPasswordHasher = new PasswordHasher<AdminAccount>();
        private readonly PasswordHasher<InspectorAccount> _inspectorPasswordHasher = new PasswordHasher<InspectorAccount>();

        public AccountManagementController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AccountManagement
        // Shows ONE combined list containing both Admin and Inspector
        // accounts, each row tagged with its Type (Admin/Inspector) so the
        // page can show a badge and send Edit/ChangePassword to the right
        // table.
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            // Pull the Admin accounts and shape them into the shared
            // AccountListItemViewModel...
            var admins = await _context.AdminAccount
                .Select(a => new AccountListItemViewModel
                {
                    Id = a.AdminAccountId,
                    Username = a.Username,
                    CreatedDate = a.CreatedDate,
                    UpdatedDate = a.UpdatedDate,
                    Type = StaffAccountType.Admin
                })
                .ToListAsync();

            // ...and do the same for the Inspector accounts.
            var inspectors = await _context.InspectorAccount
                .Select(a => new AccountListItemViewModel
                {
                    Id = a.InspectorAccountId,
                    Username = a.Username,
                    CreatedDate = a.CreatedDate,
                    UpdatedDate = a.UpdatedDate,
                    Type = StaffAccountType.Inspector
                })
                .ToListAsync();

            // Combine both lists into one, sorted by username, so the page
            // shows everything together in a single table.
            var combined = admins.Concat(inspectors)
                .OrderBy(a => a.Username)
                .ToList();

            return View(combined);
        }

        // GET: AccountManagement/Create
        // Shows the blank "create new account" form.
        //
        // NOTE: Creating new Admin accounts is NOT available from this
        // module - only Inspector accounts can be created here. This form
        // always creates an Inspector account, so the type is hardcoded
        // below rather than coming from the user.
        [HttpGet("Create")]
        public IActionResult Create()
        {
            var model = new AccountCreateViewModel { Type = StaffAccountType.Inspector };
            return View(model);
        }

        // POST: AccountManagement/Create
        // Handles the submitted "create new account" form. Always saves
        // into the InspectorAccount table - see the NOTE above.
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AccountCreateViewModel model)
        {
            // Always force Inspector here, no matter what was posted. This
            // is what actually blocks Admin account creation - it's not
            // just that the "Add Admin" button was removed from the page.
            model.Type = StaffAccountType.Inspector;

            // Manually check for a duplicate username before saving, so we
            // can show a friendly error instead of a database exception.
            if (!string.IsNullOrWhiteSpace(model.Username) && await UsernameExistsAsync(model.Type, model.Username))
            {
                ModelState.AddModelError("Username", "This username is already taken.");
            }

            if (ModelState.IsValid)
            {
                var account = new InspectorAccount
                {
                    Username = model.Username.Trim(),
                    CreatedDate = DateTime.Now
                };

                // Hash the plain-text password from the form before saving -
                // the raw password is never written to the database.
                account.PasswordHash = _inspectorPasswordHasher.HashPassword(account, model.Password);
                _context.InspectorAccount.Add(account);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Inspector account created successfully.";
                return RedirectToAction(nameof(Index));
            }

            // Something was invalid (e.g. missing field, duplicate username) -
            // redisplay the form with the validation errors.
            return View(model);
        }

        // GET: AccountManagement/Edit/Admin/5   (or Edit/Inspector/5)
        // Shows the "edit username" form for one existing account.
        [HttpGet("Edit/{type}/{id:int}")]
        public async Task<IActionResult> Edit(StaffAccountType type, int id)
        {
            var model = new AccountEditViewModel { Type = type, Id = id };

            if (type == StaffAccountType.Admin)
            {
                var account = await _context.AdminAccount.FindAsync(id);
                if (account == null) return NotFound();
                model.Username = account.Username;
            }
            else
            {
                var account = await _context.InspectorAccount.FindAsync(id);
                if (account == null) return NotFound();
                model.Username = account.Username;
            }

            return View(model);
        }

        // POST: AccountManagement/Edit/Admin/5   (or Edit/Inspector/5)
        // Handles the submitted "edit username" form. Note: this does NOT
        // touch the password - that's handled separately by ChangePassword.
        [HttpPost("Edit/{type}/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(StaffAccountType type, int id, AccountEditViewModel model)
        {
            if (id != model.Id || type != model.Type) return NotFound();

            if (!string.IsNullOrWhiteSpace(model.Username) &&
                await UsernameExistsAsync(type, model.Username, excludeId: id))
            {
                ModelState.AddModelError("Username", "This username is already taken.");
            }

            if (ModelState.IsValid)
            {
                if (type == StaffAccountType.Admin)
                {
                    var account = await _context.AdminAccount.FindAsync(id);
                    if (account == null) return NotFound();

                    account.Username = model.Username.Trim();
                    account.UpdatedDate = DateTime.Now;

                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (!await _context.AdminAccount.AnyAsync(a => a.AdminAccountId == id))
                            return NotFound();
                        throw;
                    }
                }
                else
                {
                    var account = await _context.InspectorAccount.FindAsync(id);
                    if (account == null) return NotFound();

                    account.Username = model.Username.Trim();
                    account.UpdatedDate = DateTime.Now;

                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (!await _context.InspectorAccount.AnyAsync(a => a.InspectorAccountId == id))
                            return NotFound();
                        throw;
                    }
                }

                TempData["SuccessMessage"] = $"{type} account updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: AccountManagement/ChangePassword/Admin/5  (or .../Inspector/5)
        // Shows the "set a new password" form for one existing account.
        [HttpGet("ChangePassword/{type}/{id:int}")]
        public async Task<IActionResult> ChangePassword(StaffAccountType type, int id)
        {
            var model = new AccountChangePasswordViewModel { Type = type, Id = id };

            if (type == StaffAccountType.Admin)
            {
                var account = await _context.AdminAccount.FindAsync(id);
                if (account == null) return NotFound();
                model.Username = account.Username;
            }
            else
            {
                var account = await _context.InspectorAccount.FindAsync(id);
                if (account == null) return NotFound();
                model.Username = account.Username;
            }

            return View(model);
        }

        // POST: AccountManagement/ChangePassword/Admin/5  (or .../Inspector/5)
        // Handles the submitted "set a new password" form.
        [HttpPost("ChangePassword/{type}/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(StaffAccountType type, int id, AccountChangePasswordViewModel model)
        {
            if (id != model.Id || type != model.Type) return NotFound();

            if (ModelState.IsValid)
            {
                if (type == StaffAccountType.Admin)
                {
                    var account = await _context.AdminAccount.FindAsync(id);
                    if (account == null) return NotFound();

                    account.PasswordHash = _adminPasswordHasher.HashPassword(account, model.NewPassword);
                    account.UpdatedDate = DateTime.Now;
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Password for '{account.Username}' updated successfully.";
                }
                else
                {
                    var account = await _context.InspectorAccount.FindAsync(id);
                    if (account == null) return NotFound();

                    account.PasswordHash = _inspectorPasswordHasher.HashPassword(account, model.NewPassword);
                    account.UpdatedDate = DateTime.Now;
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Password for '{account.Username}' updated successfully.";
                }

                return RedirectToAction(nameof(Index));
            }

            // Redisplay the form with the username filled back in if validation failed.
            if (type == StaffAccountType.Admin)
            {
                var existing = await _context.AdminAccount.FindAsync(id);
                if (existing != null) model.Username = existing.Username;
            }
            else
            {
                var existing = await _context.InspectorAccount.FindAsync(id);
                if (existing != null) model.Username = existing.Username;
            }

            return View(model);
        }

        // Checks whether a username is already taken (case-insensitive)
        // WITHIN the given account type, optionally excluding the account
        // currently being edited. Admin and Inspector usernames are checked
        // separately, since each table has its own unique-username rule -
        // an Admin and an Inspector are still allowed to share a username.
        private async Task<bool> UsernameExistsAsync(StaffAccountType type, string username, int? excludeId = null)
        {
            var normalized = username.Trim().ToLower();

            if (type == StaffAccountType.Admin)
            {
                if (excludeId.HasValue)
                {
                    return await _context.AdminAccount.AnyAsync(a =>
                        a.AdminAccountId != excludeId.Value && a.Username.ToLower() == normalized);
                }
                return await _context.AdminAccount.AnyAsync(a => a.Username.ToLower() == normalized);
            }
            else
            {
                if (excludeId.HasValue)
                {
                    return await _context.InspectorAccount.AnyAsync(a =>
                        a.InspectorAccountId != excludeId.Value && a.Username.ToLower() == normalized);
                }
                return await _context.InspectorAccount.AnyAsync(a => a.Username.ToLower() == normalized);
            }
        }
    }
}
