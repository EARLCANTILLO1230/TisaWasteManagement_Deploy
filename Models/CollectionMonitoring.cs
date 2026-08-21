using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TisaWasteManagement.Models
{
    public class CollectionMonitoring
    {
        [Key]
        public int CollectionMonitoringId { get; set; }

        [Required(ErrorMessage = "Schedule is required")]
        [Display(Name = "Collection Schedule")]
        public int CollectionScheduleId { get; set; }

        // "Completed" or "Delayed" — validated in the controller against the
        // spec's fixed list, same approach the existing CollectionScheduleController
        // uses for its own Status field.
        [Required(ErrorMessage = "Please select a status.")]
        [StringLength(20)]
        [Display(Name = "Collection Status")]
        public string Status { get; set; } = string.Empty;

        // Optional per the spec ("optionally adds Remarks")
        [Display(Name = "Remarks")]
        [DataType(DataType.MultilineText)]
        [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
        public string? Remarks { get; set; }

        // Comma-separated names of the sitio(s) this entry is about (e.g. "Sitio 1, Sitio 2").
        // Only set on "Collected" entries, created when one or more sitio checkboxes are
        // checked and saved on CollectionMonitoring/UpdateStatus. Stored as plain names
        // (not a link back to the Sitio table) because this is a historical log entry -
        // it should keep showing what was true at the time, even if a sitio is later renamed.
        [Display(Name = "Sitio(s)")]
        [StringLength(500)]
        public string? SitioNames { get; set; }

        // Only set on "Delayed" history entries. When a save covers multiple
        // delayed sitios with different reasons, they're combined here as
        // "SitioName: reason" pairs so the single grouped history row still
        // shows which reason belongs to which sitio.
        [Display(Name = "Reason for Delay")]
        [StringLength(1000)]
        public string? ReasonForDelay { get; set; }

        // Only set on "Reassigned" history entries (from Carry Over). Holds the
        // auto-generated reassignment description (which sitio(s) went to which
        // destination schedule) plus any additional notes the user typed in on the
        // Carry Over form. Kept separate from Remarks, which is reserved for the
        // regular Completed/Delayed remarks entered on CollectionMonitoring/Edit.
        [Display(Name = "Notes")]
        [StringLength(1000)]
        public string? Notes { get; set; }

        [Display(Name = "Log Date")]
        public DateTime LogDate { get; set; } = DateTime.Now;

        // Navigation property
        [ValidateNever]
        [ForeignKey("CollectionScheduleId")]
        public virtual CollectionSchedule CollectionSchedule { get; set; }
    }
}
