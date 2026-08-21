using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TisaWasteManagement.Data;
using TisaWasteManagement.Models;

namespace TisaWasteManagement.Services
{
    /// <summary>
    /// SMS Module
    /// -----------
    /// This class does the real work: it talks to the TextBee API to
    /// send an SMS, and it saves a log record every time (success OR
    /// failure) so nothing is silently lost.
    /// </summary>
    public class SmsService : ISmsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        // These are injected automatically by ASP.NET Core's dependency
        // injection system (registered in Program.cs).
        public SmsService(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        /// <summary>
        /// Sends one SMS message and always writes a log record.
        /// </summary>
        public async Task<bool> SendSmsAsync(string phoneNumber, string message, string notificationType, int? referenceId = null)
        {
            try
            {
                // STEP 1: Check we haven't hit today's 50-SMS limit yet.
                if (!await CanSendSmsToday())
                {
                    await LogSms(phoneNumber, message, notificationType, referenceId, "Failed", "Daily SMS limit reached (50/day).");
                    return false;
                }

                // STEP 2: Make sure the phone number looks like a valid PH mobile number.
                if (!IsValidPhilippineNumber(phoneNumber))
                {
                    await LogSms(phoneNumber, message, notificationType, referenceId, "Failed", "Invalid phone number.");
                    return false;
                }

                // STEP 3: Read the TextBee credentials from appsettings.json.
                var apiKey = _configuration["SmsSettings:TextBeeApiKey"];
                var deviceId = _configuration["SmsSettings:TextBeeDeviceId"];

                if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deviceId))
                {
                    await LogSms(phoneNumber, message, notificationType, referenceId, "Failed", "TextBee API key or Device ID not configured.");
                    return false;
                }

                // STEP 4: Convert "09171234567" into the format TextBee expects: "+639171234567".
                var formattedNumber = FormatPhoneNumber(phoneNumber);
                if (string.IsNullOrEmpty(formattedNumber))
                {
                    await LogSms(phoneNumber, message, notificationType, referenceId, "Failed", "Invalid phone number format.");
                    return false;
                }

                // STEP 5: Build and send the actual HTTP request to TextBee.
                var client = _httpClientFactory.CreateClient();
                var url = $"https://api.textbee.dev/api/v1/gateway/devices/{deviceId}/send-sms";

                // The API key must go in the request HEADER, not the body.
                client.DefaultRequestHeaders.Remove("x-api-key");
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);

                // TextBee expects "recipients" as an array, even for one number.
                var payload = new
                {
                    recipients = new[] { formattedNumber },
                    message = message
                };
                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                // STEP 6: Log the result and return whether it worked.
                if (response.IsSuccessStatusCode)
                {
                    await LogSms(phoneNumber, message, notificationType, referenceId, "Sent", responseContent);
                    return true;
                }

                await LogSms(phoneNumber, message, notificationType, referenceId, "Failed", $"HTTP {(int)response.StatusCode}: {responseContent}");
                return false;
            }
            catch (Exception ex)
            {
                // Catch-all so an unexpected error (e.g. no internet) never crashes
                // the status-update process - we just log it as failed.
                await LogSms(phoneNumber, message, notificationType, referenceId, "Failed", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Counts how many SMS were successfully sent TODAY (from midnight to now)
        /// by querying the database. This means the limit survives app restarts,
        /// unlike an in-memory counter which would reset to 0.
        /// </summary>
        private async Task<bool> CanSendSmsToday()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var count = await _context.SmsLogs
                .CountAsync(s =>
                    s.SentDate >= today &&
                    s.SentDate < tomorrow &&
                    s.Status == "Sent");

            return count < 50;
        }

        /// <summary>
        /// Saves one SmsLog row to the database. Called for every attempt,
        /// whether it succeeded or failed.
        /// </summary>
        private async Task LogSms(string phoneNumber, string message, string notificationType, int? referenceId, string status, string response)
        {
            var log = new SmsLog
            {
                RecipientNumber = phoneNumber,
                Message = message,
                NotificationType = notificationType,
                ReferenceId = referenceId,
                Status = status,
                Response = response,
                SentDate = DateTime.Now,
                SentBy = "System"
            };

            _context.SmsLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Basic check: must be exactly 11 digits and start with "09"
        /// (e.g. 09171234567). This matches the RegularExpression already
        /// used on Complaint.ContactNumber.
        /// </summary>
        private bool IsValidPhilippineNumber(string phoneNumber)
        {
            var cleaned = new string(phoneNumber.Where(char.IsDigit).ToArray());
            return cleaned.Length == 11 && cleaned.StartsWith("09");
        }

        /// <summary>
        /// Converts a local PH number into the international E.164 format
        /// that TextBee requires. Examples:
        ///   "09171234567"   -> "+639171234567"
        ///   "639171234567"  -> "+639171234567"
        /// Returns null if the number doesn't match either pattern.
        /// </summary>
        private string FormatPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return null;

            var cleaned = new string(phoneNumber.Where(char.IsDigit).ToArray());

            if (cleaned.Length == 11 && cleaned.StartsWith("09"))
            {
                return "+63" + cleaned.Substring(1);
            }

            if (cleaned.Length == 12 && cleaned.StartsWith("63"))
            {
                return "+" + cleaned;
            }

            return null;
        }

        /// <summary>
        /// Returns SMS log history for the SMS Logs page, newest first.
        /// Optionally filtered to a date range.
        /// </summary>
        public async Task<List<SmsLog>> GetLogsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.SmsLogs.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(s => s.SentDate >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(s => s.SentDate <= endDate.Value.Date.AddDays(1));

            return await query
                .OrderByDescending(s => s.SentDate)
                .ToListAsync();
        }
    }
}
