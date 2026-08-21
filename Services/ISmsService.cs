using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TisaWasteManagement.Models;

namespace TisaWasteManagement.Services
{
    /// <summary>
    /// SMS Module
    /// -----------
    /// This is the "contract" for the SMS service. Any class that
    /// implements ISmsService must provide these two methods.
    /// Using an interface (like IReportGenerator already does in this
    /// project) lets us register the service in Program.cs and inject
    /// it into controllers without controllers needing to know HOW
    /// the SMS is actually sent.
    /// </summary>
    public interface ISmsService
    {
        // Sends an SMS and logs the attempt (success or failure) to the database.
        // Returns true if the SMS was sent successfully, false otherwise.
        Task<bool> SendSmsAsync(string phoneNumber, string message, string notificationType, int? referenceId = null);

        // Retrieves SMS log history, optionally filtered by a date range.
        Task<List<SmsLog>> GetLogsAsync(DateTime? startDate = null, DateTime? endDate = null);
    }
}
