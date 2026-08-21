// Helpers/RequireStaffRoleAttribute.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace TisaWasteManagement.Helpers
{
    // Temporary Phase 1 access guard - checks the hardcoded Session role set by
    // AccountController.Login instead of full ASP.NET Identity/cookie auth.
    // Apply as [RequireStaffRole("Admin")] or [RequireStaffRole("Inspector")]
    // on any controller/action that should only be reachable after Staff Login.
    // Can also take more than one allowed role, e.g. [RequireStaffRole("Admin", "Inspector")]
    // for pages both roles are allowed to use.
    public class RequireStaffRoleAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _roles;

        public RequireStaffRoleAttribute(params string[] roles)
        {
            _roles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // If this specific action is marked [AllowAnonymous] (e.g. a public,
            // resident-facing page on an otherwise staff-only controller), skip
            // the role check entirely and let anyone view it.
            bool isAnonymousAllowed = context.ActionDescriptor.EndpointMetadata
                .Any(m => m is AllowAnonymousAttribute);
            if (isAnonymousAllowed)
            {
                return;
            }

            var sessionRole = context.HttpContext.Session.GetString("StaffRole");
            if (string.IsNullOrEmpty(sessionRole) || !_roles.Contains(sessionRole))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
            }
        }
    }
}