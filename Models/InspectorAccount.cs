using System;
using System.ComponentModel.DataAnnotations;

namespace TisaWasteManagement.Models
{
    /// <summary>
    /// Login credentials for an Inspector staff account. Created, edited, and
    /// password-reset by Admin via InspectorAccountController.
    ///
    /// Replaces the old hardcoded "inspector" / "inspector" credentials that
    /// used to live directly inside AccountController.Login.
    /// </summary>
    public class InspectorAccount
    {
        [Key]
        public int InspectorAccountId { get; set; }

        [Required(ErrorMessage = "Username is required.")]
        [StringLength(50, ErrorMessage = "Username cannot exceed 50 characters.")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        // Only ever store the one-way hash produced by ASP.NET Core's
        // PasswordHasher<T> - never the raw password itself. A hash cannot be
        // turned back into the original password by anyone, which is why
        // there is no "view password" feature - only "reset password".
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "Last Updated")]
        public DateTime? UpdatedDate { get; set; }
    }
}
