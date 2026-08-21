using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Collections.Generic;

namespace TisaWasteManagement.Models
{
    public class CollectionSchedule
    {
        [Key]
        public int CollectionScheduleId { get; set; }

        // Removed single SitioId to allow multiple Sitios per schedule.
        // public int SitioId { get; set; }

        // A schedule has exactly ONE Collector and ONE GarbageTruck (see CollectorId/
        // TruckId below). Sitios are still many-to-many via CollectionScheduleSitio.

        [Required(ErrorMessage = "Please select a collection day.")]
        [Display(Name = "Collection Day")]
        public DayOfWeek? DayOfWeek { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending";

        // Dump Number tells apart multiple collection routes made by the SAME truck on the
        // SAME day (e.g. a truck's morning route is Dump 1, its afternoon route is Dump 2).
        // Defaults to 1 because most schedules are just a single route for that truck/day.
        [Required(ErrorMessage = "Please enter a dump number.")]
        [Display(Name = "Dump Number")]
        [Range(1, 99, ErrorMessage = "Dump number must be between 1 and 99.")]
        public int DumpNumber { get; set; } = 1;

        [Display(Name = "Created Date")]
        public DateTime? CreatedDate { get; set; }

        [Display(Name = "Updated Date")]
        public DateTime? UpdatedDate { get; set; }

        [Display(Name = "Date of Completion")]
        public DateTime? DateOfCompletion { get; set; }

        // Whether this schedule should automatically repeat weekly after completion.
        [Display(Name = "Repeat Weekly")]
        public bool RepeatWeekly { get; set; } = false;

        // --- Lineage tracking for auto-generated recurring occurrences ---
        //
        // When a RepeatWeekly schedule is marked Completed, the system creates a NEW
        // CollectionSchedule row for the next week's occurrence (see UpdateStatus in
        // CollectionScheduleController / CollectionMonitoringController). Before this
        // field existed, that new row had no recorded link back to the schedule that
        // created it - the system could only guess two rows were related by noticing
        // they shared the same Sitio and DayOfWeek, which caused bugs (e.g. editing a
        // Completed schedule getting blocked by "Sitio already scheduled" because of
        // its own auto-generated successor).
        //
        // ParentScheduleId makes that link explicit and queryable:
        // - null   => this schedule was created directly by a user (via Create/Edit).
        // - not null => this schedule was auto-generated as the "next occurrence"
        //               of the schedule whose CollectionScheduleId is stored here.
        [Display(Name = "Parent Schedule")]
        public int? ParentScheduleId { get; set; }

        // Navigation to the schedule this one was generated from (if any).
        [ValidateNever]
        [ForeignKey(nameof(ParentScheduleId))]
        public virtual CollectionSchedule? ParentSchedule { get; set; }

        // Navigation to every schedule that was generated FROM this one (normally
        // at most one at a time, since a schedule only spawns its next occurrence
        // once it's Completed, but modeled as a collection in case that ever changes).
        [ValidateNever]
        public virtual ICollection<CollectionSchedule> ChildSchedules { get; set; } = new List<CollectionSchedule>();

        // Note field - OPTIONAL (nullable)
        [Display(Name = "Notes")]
        [DataType(DataType.MultilineText)]
        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Note { get; set; }

        // Navigation properties
        [ValidateNever]
        // Collection of join entities that link this schedule to multiple Sitios
        public virtual ICollection<CollectionScheduleSitio> CollectionScheduleSitios { get; set; } = new List<CollectionScheduleSitio>();

        // A schedule now has exactly ONE collector (previously many-to-many via
        // CollectionScheduleCollector). Same pattern as TruckId/GarbageTruck below.
        [Display(Name = "Driver")]
        public int? DriverId { get; set; }

        [ValidateNever]
        [ForeignKey(nameof(DriverId))]
        public virtual Collector? Driver { get; set; }

        // A schedule can have MULTIPLE collectors (the crew riding along with the driver),
        // linked through the CollectionScheduleCollector join table - same many-to-many
        // pattern already used for Sitios above.
        [ValidateNever]
        public virtual ICollection<CollectionScheduleCollector> CollectionScheduleCollectors { get; set; } = new List<CollectionScheduleCollector>();

        // A schedule now has exactly ONE garbage truck (previously many-to-many via
        // CollectionScheduleTruck). Combined with DumpNumber above, the same truck can
        // still appear on multiple schedules for the same day - each one is a separate route.
        [Display(Name = "Garbage Truck")]
        public int? TruckId { get; set; }

        [ValidateNever]
        [ForeignKey(nameof(TruckId))]
        public virtual GarbageTruck? GarbageTruck { get; set; }
    }
}
