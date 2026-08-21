// Models/BulletinBoardImage.cs
using System.ComponentModel.DataAnnotations;

namespace TisaWasteManagement.Models
{
    // Represents a single picture on the public Bulletin Board
    // (e.g. waste segregation posters, recycling guides, other
    // educational/informational images).
    public class BulletinBoardImage
    {
        public int BulletinBoardImageId { get; set; }

        // We only store the FILE NAME here (e.g. "a1b2c3d4.jpg"), not the full path.
        // The picture itself lives in wwwroot/images/bulletinboard/.
        // The full web path is built in the views as: /images/bulletinboard/{ImageFileName}
        [Required]
        public string ImageFileName { get; set; } = string.Empty;

        // Optional short caption shown under the picture (e.g. "Proper Waste Segregation").
        [StringLength(150)]
        public string? Caption { get; set; }

        public DateTime DateUploaded { get; set; } = DateTime.Now;
    }
}
