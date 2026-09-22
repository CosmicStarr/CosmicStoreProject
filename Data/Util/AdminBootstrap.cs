using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Models;

namespace Data.Util;

/// <summary>
/// Bootstrap Admin role and config-driven admin emails (Store:AdminEmails).
/// </summary>
public static class AdminBootstrap
{
    public static IReadOnlyList<string> AdminEmails(IConfiguration configuration) =>
        configuration.GetSection("Store:AdminEmails").Get<string[]>()?
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];

    public static bool IsBootstrapAdmin(IConfiguration configuration, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return AdminEmails(configuration)
            .Any(admin => string.Equals(admin, email.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static async Task EnsureAdminRoleAsync(RoleManager<IdentityRole> roleManager)
    {
        if (!await roleManager.RoleExistsAsync(StaticInfo.AdminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(StaticInfo.AdminRole));
        }
    }

    public static async Task AssignBootstrapAdminAsync(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppUser user)
    {
        await EnsureAdminRoleAsync(roleManager);
        if (!await userManager.IsInRoleAsync(user, StaticInfo.AdminRole))
        {
            await userManager.AddToRoleAsync(user, StaticInfo.AdminRole);
        }
    }
}
