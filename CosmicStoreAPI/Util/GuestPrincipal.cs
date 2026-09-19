using System.Security.Claims;
using Data.Util;

namespace CosmicStoreAPI.Util;

/// <summary>Guest checkout is a JWT session only — no Identity user is stored.</summary>
public static class GuestPrincipal
{
    public const string GuestClaimType = "guest";

    public static bool IsGuest(ClaimsPrincipal principal) =>
        principal.IsInRole(StaticInfo.GuestRole)
        || principal.HasClaim("role", StaticInfo.GuestRole)
        || principal.HasClaim(GuestClaimType, "true");
}
