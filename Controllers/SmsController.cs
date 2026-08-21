using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using TisaWasteManagement.Data;
using TisaWasteManagement.Helpers;
using TisaWasteManagement.Services;

namespace TisaWasteManagement.Controllers
{
    /// <summary>
    /// SMS Module
    /// -----------
    /// Admin-only controller that shows the SMS Logs page, so Admins
    /// can see a history of every SMS the system tried to send.
    /// Uses the same [RequireStaffRole] pattern already used by
    /// ComplaintManagementController.
    /// </summary>
    [RequireStaffRole("Admin")]
    public class SmsController : Controller
    {
        private readonly ISmsService _smsService;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public SmsController(ISmsService smsService, IConfiguration configuration, ApplicationDbContext context)
        {
            _smsService = smsService;
            _configuration = configuration;
            _context = context;
        }

        /// <summary>
        /// GET: Sms/Index
        /// Shows the list of SMS logs, plus simple Sent/Failed counters.
        /// </summary>
        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate)
        {
            var logs = await _smsService.GetLogsAsync(startDate, endDate);

            // Simple stats shown at the top of the page.
            ViewBag.TotalSent = await _context.SmsLogs.CountAsync(s => s.Status == "Sent");
            ViewBag.TotalFailed = await _context.SmsLogs.CountAsync(s => s.Status == "Failed");
            ViewBag.Provider = _configuration["SmsSettings:Provider"] ?? "TextBee";

            return View(logs);
        }
    }
}
