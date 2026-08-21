using TisaWasteManagement.Models;

namespace TisaWasteManagement.Helpers
{
    // Generic weekly-recurrence date math: "given a selected weekday (and
    // optionally a stored occurrence date / repeat flag), what date should
    // this thing next happen on, and what date should the UI display?"
    //
    // The core methods take plain values so they can be reused by anything
    // that recurs weekly - not just CollectionSchedule (e.g. truck maintenance,
    // driver shifts, etc.). CollectionSchedule-specific overloads are kept as
    // thin wrappers below for backward compatibility with existing callers.
    public static class WeeklyRecurrenceHelper
    {
        // Calculate the next occurrence date for a given DayOfWeek relative to "today".
        // Rules:
        // - If selected weekday == today's weekday => return today
        // - If selected weekday is later this week   => return that date this week
        // - If selected weekday already passed this week => return that date next week
        public static DateTime CalculateScheduledDate(DayOfWeek selectedDay, DateTime? referenceDate = null)
        {
            var today = (referenceDate ?? DateTime.Today).Date;
            var todayDow = (int)today.DayOfWeek;
            var target = (int)selectedDay;
            var daysToAdd = ((target - todayDow) + 7) % 7; // 0 means same day
            return today.AddDays(daysToAdd);
        }

        // Generic version: get the "next occurrence" date to use for sorting/display,
        // given a stored occurrence date (if any) and/or a target weekday.
        // Uses storedDate when present (it holds the actual scheduled occurrence),
        // otherwise falls back to computing it from dayOfWeek.
        public static DateTime GetNextOccurrenceDate(DateTime? storedDate, DayOfWeek? dayOfWeek, DateTime? referenceDate = null)
        {
            if (storedDate.HasValue)
                return storedDate.Value.Date;

            if (dayOfWeek.HasValue)
                return CalculateScheduledDate(dayOfWeek.Value, referenceDate);

            return DateTime.MaxValue;
        }

        // Generic version: determine the date to *display* for a recurring item.
        //
        // BUG FIX (see history): this method used to ALWAYS recompute a "next
        // occurrence" date from today whenever the item wasn't a still-upcoming
        // repeat occurrence - and it never looked at Status. That meant a
        // COMPLETED schedule's date got silently recalculated to "today" (or
        // whatever the next matching weekday was) instead of showing the date
        // it actually happened on. We now check `status` first: once something
        // is Completed, its storedDate IS the historical record and must be
        // shown exactly as-is, never recalculated.
        //
        // Rules (in order):
        // - status == "Completed": ALWAYS show the original storedDate as-is.
        //   A completed record represents something that already happened, so
        //   its date must never move.
        // - Repeating items whose stored date is a future auto-generated occurrence
        //   show that stored date.
        // - Everything else (manually created, or repeating items whose stored date
        //   is today/in the past) shows the next occurrence of the selected weekday
        //   computed from today, so the display never shows a stale past date.
        public static DateTime GetDisplayDate(DateTime? storedDate, DayOfWeek? dayOfWeek, bool repeatsWeekly, string status, DateTime? referenceDate = null)
        {
            var today = (referenceDate ?? DateTime.Today).Date;

            // storedDate = the date the schedule actually happened / was created for.
            // Once Completed, this is the only value we trust - no recalculation.
            if (status == "Completed" && storedDate.HasValue)
            {
                return storedDate.Value.Date;
            }

            if (repeatsWeekly && storedDate.HasValue && storedDate.Value.Date > today)
            {
                return storedDate.Value.Date;
            }

            if (dayOfWeek.HasValue)
            {
                return CalculateScheduledDate(dayOfWeek.Value, today);
            }

            return today;
        }

        // Map status to a priority integer for secondary ordering (lower = higher priority).
        // Kept here since it's commonly sorted alongside occurrence date, though it's
        // not itself date math.
        public static int GetStatusPriority(string status)
        {
            return status == "Pending" ? 0 : status == "Delayed" ? 1 : status == "Completed" ? 3 : 2;
        }

        // --- CollectionSchedule-specific convenience overloads ---
        // Thin wrappers so existing controller/view code doesn't need to change.

        public static DateTime GetNextOccurrenceDate(CollectionSchedule schedule, DateTime? referenceDate = null)
        {
            if (schedule == null)
                return DateTime.MaxValue;

            return GetNextOccurrenceDate(schedule.CreatedDate, schedule.DayOfWeek, referenceDate);
        }

        public static DateTime GetDisplayDate(CollectionSchedule schedule, DateTime? referenceDate = null)
        {
            if (schedule == null)
                return (referenceDate ?? DateTime.Today).Date;

            // Passing schedule.Status through is the actual fix: it lets GetDisplayDate
            // know this schedule is Completed so it stops recalculating the date.
            return GetDisplayDate(schedule.CreatedDate, schedule.DayOfWeek, schedule.RepeatWeekly, schedule.Status, referenceDate);
        }
    }
}