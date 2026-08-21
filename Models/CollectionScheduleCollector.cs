using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TisaWasteManagement.Models
{
    public class CollectionScheduleCollector
    {
        [Key]
        public int CollectionScheduleCollectorId { get; set; }

        [Required]
        public int CollectionScheduleId { get; set; }

        [Required]
        public int CollectorId { get; set; }

        [ForeignKey("CollectionScheduleId")]
        public virtual CollectionSchedule CollectionSchedule { get; set; }

        [ForeignKey("CollectorId")]
        public virtual Collector Collector { get; set; }
    }
}
