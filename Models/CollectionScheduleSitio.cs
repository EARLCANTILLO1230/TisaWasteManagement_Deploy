using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TisaWasteManagement.Models
{
    public class CollectionScheduleSitio
    {
        [Key]
        public int CollectionScheduleSitioId { get; set; }

        [Required]
        public int CollectionScheduleId { get; set; }

        [Required]
        public int SitioId { get; set; }

        [ForeignKey("CollectionScheduleId")]
        public virtual CollectionSchedule CollectionSchedule { get; set; }

        [ForeignKey("SitioId")]
        public virtual Sitio Sitio { get; set; }

        // The collection status for this specific sitio, set on the
        // CollectionMonitoring/Edit page. "Pending" until the user records an
        // outcome for it; after that it's either "Completed" or "Delayed" -
        // the user must pick exactly one, there is no "uncollected" checkbox
        // anymore. The CollectionSchedule/Details page uses this to decide
        // whether the schedule as a whole can be marked "Completed" (every
        // sitio must be "Completed" first).
        [Display(Name = "Status")]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        // Required whenever Status is "Delayed" (enforced in the controller,
        // same approach as the rest of the app's status validation). Kept null
        // for "Pending"/"Completed" sitios.
        [Display(Name = "Reason for Delay")]
        [StringLength(500)]
        public string? ReasonForDelay { get; set; }

        // Set (to the destination schedule's Id) when this sitio was carried over to
        // another schedule via CollectionSchedule/CarryOver. The row is kept - not
        // deleted - specifically so that a Repeat Weekly schedule's "copy my current
        // sitios to next week's occurrence" step still picks this sitio up: next
        // week's schedule should always reflect the ORIGINAL configuration, regardless
        // of any carry-over that happened this week. Once reassigned, this sitio is
        // hidden from this schedule's active checklist (CollectionMonitoring/Edit) and
        // Carry Over list, since it's no longer this team's responsibility.
        [Display(Name = "Reassigned To Schedule")]
        public int? ReassignedToScheduleId { get; set; }
    }
}
