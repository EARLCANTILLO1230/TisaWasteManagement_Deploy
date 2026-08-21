// Models/ReportViewModel.cs
//
// This is the "form model" for the Reports page. It holds whatever the
// Admin/Inspector picks (report type, date range, filters, export format)
// AND the dropdown option lists that the form needs to display.
//
// NOTE: There are TWO status dropdowns (CollectionStatusOptions and
// ComplaintStatusOptions) because Collection Schedules and Complaints use
// different status words:
//   - Collection status:  Pending / Completed / Delayed
//   - Complaint status:   Awaiting Review / Ongoing / Accomplished
// The view (Index.cshtml) only shows ONE of these two dropdowns at a time,
// depending on which Report Type is selected, using simple JavaScript.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TisaWasteManagement.Models
{
    public class ReportViewModel
    {
        [Required(ErrorMessage = "Please select a report type.")]
        [Display(Name = "Report Type")]
        public string ReportType { get; set; } = "Collection"; // "Collection" or "Complaint"

        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        // Holds whichever status value the user picked (from either dropdown).
        [Display(Name = "Status Filter")]
        public string? StatusFilter { get; set; }

        [Display(Name = "Sitio Filter")]
        public int? SitioId { get; set; }

        [Required(ErrorMessage = "Please select an export format.")]
        [Display(Name = "Export Format")]
        public string ExportFormat { get; set; } = "PDF"; // "PDF" or "Excel"

        // ----- Dropdown lists filled in by the controller before showing the form -----
        public List<SelectListItem> ReportTypes { get; set; } = new();
        public List<SelectListItem> CollectionStatusOptions { get; set; } = new();
        public List<SelectListItem> ComplaintStatusOptions { get; set; } = new();
        public List<SelectListItem> SitioOptions { get; set; } = new();
        public List<SelectListItem> ExportFormats { get; set; } = new();
    }
}
