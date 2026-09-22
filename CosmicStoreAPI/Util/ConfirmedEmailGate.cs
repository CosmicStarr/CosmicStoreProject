using System.Security.Claims;
using Data.Util;
using Microsoft.AspNetCore.Identity;
using Models;

namespace CosmicStoreAPI.Util;

/// <summary>Blocks purchase endpoints until the signed-in Identity user has confirmed their email.</summary>
public static class ConfirmedEmailGate
{
    public const string RequiredMessage =
        "Confirm your email to purchase with this account, or check out as a guest.";

    public static async Task<string?> UnconfirmedMessageAsync(
        UserManager<AppUser> userManager,
        ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true || GuestPrincipal.IsGuest(principal))
        {
            return null;
        }

        var email = principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return RequiredMessage;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return RequiredMessage;
        }

        if (user.EmailConfirmed || await userManager.IsInRoleAsync(user, StaticInfo.GuestRole))
        {
            return null;
        }

        // Signed-in accounts may still check out. The order is attached to the user
        // even when the confirmation email is outstanding.
        return null;
    }
}
