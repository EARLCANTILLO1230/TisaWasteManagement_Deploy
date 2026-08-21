using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TisaWasteManagement.Models
{
    /// <summary>
    /// ViewModel for the Complaint Management dashboard.
    /// Contains all data needed for the admin/inspector view.
    /// </summary>
    public class ComplaintManagementViewModel
    {
        // List of complaints to display
        public List<Complaint> Complaints { get; set; } = new List<Complaint>();

        // Statistics for the dashboard cards
        public ComplaintStatistics Statistics { get; set; } = new ComplaintStatistics();

        // Filter values
        public string SearchKeyword { get; set; } = string.Empty;
        public string StatusFilter { get; set; } = string.Empty;
    }

    /// <summary>
    /// Statistics for the complaint dashboard.
    /// </summary>
    public class ComplaintStatistics
    {
        public int TotalCount { get; set; }
        public int AwaitingReviewCount { get; set; } // Renamed from UrgentCount - now counts complaints with "Awaiting Review" status
        public int OngoingCount { get; set; }
        public int AccomplishedCount { get; set; }
    }

    /// <summary>
    /// ViewModel for resolving/updating a complaint status.
    /// </summary>
    public class ResolveComplaintViewModel
    {
        public int ComplaintId { get; set; }

        [Required(ErrorMessage = "Please select a status.")]
        [Display(Name = "Status")]
        public string SelectedStatus { get; set; } = string.Empty;

        public Complaint Complaint { get; set; }

        // Available status options for dropdown
        public List<StatusOption> StatusOptions { get; set; } = new List<StatusOption>();
    }

    /// <summary>
    /// Status option for dropdown list.
    /// </summary>
    public class StatusOption
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}