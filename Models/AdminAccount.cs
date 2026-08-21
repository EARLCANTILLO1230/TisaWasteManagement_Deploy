using System;
using System.ComponentModel.DataAnnotations;

namespace TisaWasteManagement.Models
{
    /// <summary>
    /// Login credentials for an Admin staff account. Created, edited, and
    /// password-reset by another Admin via AdminAccountController.
    ///
    /// Replaces the old hardcoded "admin" / "Admin" credentials that used
    /// to live directly inside AccountController.Login.
    ///
    /// This is a straight copy of the InspectorAccount pattern - same shape,
    /// same password hashing, same "no plain-text password stored" rule.
    /// </summary>
    public class AdminAccount
    {
        [Key]
        public int AdminAccountId { get; set; }

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
