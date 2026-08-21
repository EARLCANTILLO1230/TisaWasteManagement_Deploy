//// Controllers/AccountController.cs
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.EntityFrameworkCore;
//using System.Threading.Tasks;
//using TisaWasteManagement.Data;
//using TisaWasteManagement.Models;

//namespace TisaWasteManagement.Controllers
//{
//    // Phase 2: Admin login now checks the AdminAccount table instead of a
//    // hardcoded username/password - same as Inspector login already does.
//    // Admin creates/edits Admin accounts via AdminAccountController, and
//    // Inspector accounts via InspectorAccountController (unchanged).
//    public class AccountController : Controller
//    {
//        private readonly ApplicationDbContext _context;

//        // One PasswordHasher per account type, since PasswordHasher<T> is
//        // generic. This mirrors the existing InspectorAccount hasher below -
//        // just pointed at AdminAccount instead.
//        private readonly PasswordHasher<AdminAccount> _adminPasswordHasher = new PasswordHasher<AdminAccount>();
//        private readonly PasswordHasher<InspectorAccount> _passwordHasher = new PasswordHasher<InspectorAccount>();

//        public AccountController(ApplicationDbContext context)
//        {
//            _context = context;
//        }

//        // GET: Account/Login
//        public IActionResult Login()
//        {
//            // If already logged in, redirect to appropriate dashboard
//            var role = HttpContext.Session.GetString("StaffRole");
//            if (!string.IsNullOrEmpty(role))
//            {
//                return role == "Admin" ? RedirectToAction("Index", "Admin") : RedirectToAction("Index", "Inspector");
//            }
//            return View();
//        }

//        // POST: Account/Login
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Login(string username, string password)
//        {
//            // Admin credentials now come from the database instead of being
//            // hardcoded - see AdminAccount / AdminAccountController.
//            var admin = await _context.AdminAccount
//                .FirstOrDefaultAsync(a => a.Username == username);

//            if (admin != null)
//            {
//                var adminResult = _adminPasswordHasher.VerifyHashedPassword(admin, admin.PasswordHash, password ?? string.Empty);
//                if (adminResult == PasswordVerificationResult.Success || adminResult == PasswordVerificationResult.SuccessRehashNeeded)
//                {
//                    HttpContext.Session.SetString("StaffRole", "Admin");
//                    return RedirectToAction("Index", "Admin");
//                }
//            }

//            // Inspector credentials come from the database - see
//            // InspectorAccount / InspectorAccountController (unchanged).
//            var inspector = await _context.InspectorAccount
//                .FirstOrDefaultAsync(a => a.Username == username);

//            if (inspector != null)
//            {
//                var result = _passwordHasher.VerifyHashedPassword(inspector, inspector.PasswordHash, password ?? string.Empty);
//                if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
//                {
//                    HttpContext.Session.SetString("StaffRole", "Inspector");
//                    return RedirectToAction("Index", "Inspector");
//                }
//            }

//            // Invalid login
//            ViewBag.Error = "Invalid username or password.";
//            return View();
//        }

//        // GET: Account/Logout
//        public IActionResult Logout()
//        {
//            HttpContext.Session.Clear();
//            return RedirectToAction("Login");
//        }
//    }
//}
