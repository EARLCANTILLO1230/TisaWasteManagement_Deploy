using System.Collections.Generic;

namespace TisaWasteManagement.Models
{
    /// <summary>
    /// ViewModel for the Records Management dashboard.
    /// Contains both collection and complaint records.
    /// This module serves as the centralized repository for historical records.
    /// All records are read-only - no editing or deletion.
    /// </summary>
    public class RecordsManagementViewModel
    {
        // Collection records from CollectionSchedule and MonitoringLog
        public List<CollectionSchedule> CollectionRecords { get; set; } = new List<CollectionSchedule>();

        // Complaint records from Complaint table
        public List<Complaint> ComplaintRecords { get; set; } = new List<Complaint>();

        // Total count of collection records
        public int CollectionCount { get; set; }

        // Total count of complaint records
        public int ComplaintCount { get; set; }
    }
}