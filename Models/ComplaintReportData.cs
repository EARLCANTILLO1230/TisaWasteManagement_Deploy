// Models/ComplaintReportData.cs
//
// Same idea as CollectionReportData - a plain container object (not saved
// to the database) that we fill with numbers and hand to ReportGenerator.
//
// NOTE: Complaint.Status in this project uses these 3 values:
//   "Awaiting Review", "Ongoing", "Accomplished"
// (NOT "Urgent" - that's why this uses AwaitingReviewCount instead.)
namespace TisaWasteManagement.Models
{
    public class ComplaintReportData
    {
        public DateTime GeneratedDate { get; set; } = DateTime.Now;
        public string GeneratedBy { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public int TotalComplaints { get; set; }
        public int AwaitingReviewCount { get; set; }
        public int OngoingCount { get; set; }
        public int AccomplishedCount { get; set; }

        // Breakdown by complaint type (Missed Collection, Illegal Dumping, Other, ...)
        public List<ComplaintTypeSummary> ComplaintTypeSummaries { get; set; } = new();

        // Breakdown per sitio - also used to work out "Frequently Affected Areas"
        public List<SitioComplaintSummary> SitioSummaries { get; set; } = new();

        // Monthly trend - how many complaints were filed each month (grouped
        // by FiledDate), plus the % change from the previous month so the
        // reader can see at a glance whether complaints are rising or falling.
        public List<MonthlyTrend> Trends { get; set; } = new();
    }

    public class ComplaintTypeSummary
    {
        public string ComplaintType { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class SitioComplaintSummary
    {
        public string SitioName { get; set; } = string.Empty;
        public int Total { get; set; }
        public int AwaitingReview { get; set; }
        public int Ongoing { get; set; }
        public int Accomplished { get; set; }
        public double PercentageOfTotal { get; set; }
    }

    public class MonthlyTrend
    {
        // Display-friendly label, e.g. "August 2026"
        public string MonthYear { get; set; } = string.Empty;
        public int Complaints { get; set; }

        // Null for the very first month in the list (nothing to compare it to).
        // Otherwise, e.g. +27.0 means "27% more complaints than last month".
        public double? PercentChangeFromPreviousMonth { get; set; }
    }
}
