using System;
using System.ComponentModel.DataAnnotations;

namespace TisaWasteManagement.Models
{
    // Which staff table an account belongs to. The AdminAccount and
    // InspectorAccount database tables are still two separate tables (see
    // AdminAccount.cs / InspectorAccount.cs) - this enum is just how the
    // combined "Account Management" pages remember which table a given
    // account came from, so one controller/set of views can handle both.
    public enum StaffAccountType
    {
        Admin,
        Inspector
    }

    // These "ViewModel" classes are NOT database tables - they only shape
    // the data that comes from (and goes to) the Index/Create/Edit/
    // ChangePassword pages. This replaces the old AdminAccountViewModels.cs
    // and InspectorAccountViewModels.cs - same fields as before, just with
    // a "Type" added so the same form can create/edit either kind of
    // account.

    // One row shown on the combined Index list. Mixes together accounts
    // from both the AdminAccount and InspectorAccount tables.
    public class AccountListItemViewModel
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public StaffAccountType Type { get; set; }
    }

    public class AccountCreateViewModel
    {
        // Decides whether Create() saves into AdminAccount or
        // InspectorAccount. Set from the "Add Admin" / "Add Inspector"
        // button the user clicked on the Index page.
        [Required]
        public StaffAccountType Type { get; set; }

        [Required(ErrorMessage = "Username is required.")]
        [StringLength(50, ErrorMessage = "Username cannot exceed 50 characters.")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm the password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class AccountEditViewModel
    {
        public int Id { get; set; }
        public StaffAccountType Type { get; set; }

        [Required(ErrorMessage = "Username is required.")]
        [StringLength(50, ErrorMessage = "Username cannot exceed 50 characters.")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;
    }

    public class AccountChangePasswordViewModel
    {
        public int Id { get; set; }
        public StaffAccountType Type { get; set; }

        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "New password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm the new password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
