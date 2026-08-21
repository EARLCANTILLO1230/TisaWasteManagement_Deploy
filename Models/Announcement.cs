// Models/Announcement.cs
using System.ComponentModel.DataAnnotations;

namespace TisaWasteManagement.Models
{
    // Represents a single announcement that an Admin creates and publishes
    // on the public Home page.
    public class Announcement
    {
        public int AnnouncementId { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Content is required.")]
        [DataType(DataType.MultilineText)]
        public string Content { get; set; } = string.Empty;

        // We only store the FILE NAME here (e.g. "3f6b1c2e.jpg"), not the full path.
        // The picture itself lives in wwwroot/images/announcements/.
        // The full web path is built in the views as: /images/announcements/{ImageFileName}
        public string? ImageFileName { get; set; }

        // When the announcement was posted. Defaults to "now" so the admin
        // doesn't have to fill this in manually when creating one.
        public DateTime DatePosted { get; set; } = DateTime.Now;
    }
}
