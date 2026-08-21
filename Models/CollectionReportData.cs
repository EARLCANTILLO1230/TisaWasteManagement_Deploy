// Models/CollectionReportData.cs
//
// This is NOT saved to the database - it's just a plain container object
// that we fill with numbers pulled from the database, and then hand to
// ReportGenerator so it can turn it into a PDF or Excel file.
//
// IMPORTANT: We build this report from CollectionScheduleSitio records
// (not directly from CollectionSchedule). That's because a single
// CollectionSchedule can cover MULTIPLE sitios, and each sitio has its
// OWN status (Pending/Completed/Delayed) stored on CollectionScheduleSitio.
// So "1 row of CollectionScheduleSitio" = "1 sitio's collection status
// within 1 schedule", which is the actual thing we're counting/reporting on.
namespace TisaWasteManagement.Models
{
    public class CollectionReportData
    {
        public DateTime GeneratedDate { get; set; } = DateTime.Now;
        public string GeneratedBy { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Overall totals across every CollectionScheduleSitio record that matched the filters
        public int TotalCount { get; set; }
        public int CompletedCount { get; set; }
        public int DelayedCount { get; set; }
        public int PendingCount { get; set; }

        // Breakdown per sitio (used for the "Schedules by Sitio" table and
        // the "Sitios with Most Delays" list)
        public List<SitioCollectionSummary> SitioSummaries { get; set; } = new();

        // Monthly trend - how many records fell into each month, grouped by
        // the schedule's CreatedDate. Lets the reader see whether collection
        // activity/delays are going up or down over time.
        public List<MonthlyCollectionTrend> Trends { get; set; } = new();
    }

    public class SitioCollectionSummary
    {
        public string SitioName { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Delayed { get; set; }
        public int Pending { get; set; }

        // Calculated automatically from the counts above - no need to set this manually.
        public double DelayRate => Total > 0 ? (double)Delayed / Total * 100 : 0;
    }

    public class MonthlyCollectionTrend
    {
        // Display-friendly label, e.g. "August 2026"
        public string MonthYear { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Delayed { get; set; }
        public int Pending { get; set; }
    }
}
