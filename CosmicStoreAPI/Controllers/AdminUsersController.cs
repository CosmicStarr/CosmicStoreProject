using System.Security.Claims;
using System.Text;
using Data.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Models;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Admin Identity user management: list, roles, lock, unlock, reset password, delete.
/// </summary>
[Authorize(Roles = "Admin")]
public class AdminUsersController(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IEmailSender emailSender,
    IWebHostEnvironment env,
    IConfiguration configuration) : BaseController
{
    private readonly UserManager<AppUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly IWebHostEnvironment _env = env;
    private readonly IConfiguration _configuration = configuration;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminUserDto>>> GetUsers()
    {
        var users = _userManager.Users.OrderBy(user => user.Email).ToList();
        var result = new List<AdminUserDto>(users.Count);

        foreach (var user in users)
        {
            result.Add(await ToDtoAsync(user));
        }

        return Ok(result);
    }

    [HttpPost("{id}/roles/admin")]
    public async Task<ActionResult<AdminUserDto>> GrantAdmin(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        await AdminBootstrap.EnsureAdminRoleAsync(_roleManager);
        if (!await _userManager.IsInRoleAsync(user, StaticInfo.AdminRole))
        {
            var result = await _userManager.AddToRoleAsync(user, StaticInfo.AdminRole);
            if (!result.Succeeded)
            {
                return BadRequest(new { message = string.Join(" ", result.Errors.Select(error => error.Description)) });
            }
        }

        return Ok(await ToDtoAsync(user));
    }

    [HttpDelete("{id}/roles/admin")]
    public async Task<ActionResult<AdminUserDto>> RevokeAdmin(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (!await _userManager.IsInRoleAsync(user, StaticInfo.AdminRole))
        {
            return Ok(await ToDtoAsync(user));
        }

        if (await CountAdminsAsync() <= 1)
        {
            return BadRequest(new { message = "You cannot remove the last Admin role." });
        }

        var result = await _userManager.RemoveFromRoleAsync(user, StaticInfo.AdminRole);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join(" ", result.Errors.Select(error => error.Description)) });
        }

        return Ok(await ToDtoAsync(user));
    }

    [HttpPost("{id}/lock")]
    public async Task<ActionResult<AdminUserDto>> LockUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.Equals(user.Id, actorId, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "You cannot lock your own account." });
        }

        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, AccountLockouts.PermanentEnd);
        await _userManager.UpdateSecurityStampAsync(user);
        return Ok(await ToDtoAsync(user));
    }

    [HttpPost("{id}/unlock")]
    public async Task<ActionResult<object>> UnlockUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return BadRequest(new { message = "This account has no email address for the required password-reset message." });
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = BuildResetUrl(token, user.Id, user.Email);
        var html = await LoadResetEmailAsync(resetUrl);
        var resetEmailSent = true;

        try
        {
            await _emailSender.SendEmailAsync(user.Email, "Reset your CosmicStore password", html);
        }
        catch (Exception)
        {
            resetEmailSent = false;
        }

        var dto = await ToDtoAsync(user);
        if (!resetEmailSent)
        {
            return StatusCode(503, new
            {
                message = $"Unlocked {user.Email}, but the password-reset email could not be sent. Use Reset password to try again.",
                user = dto
            });
        }

        return Ok(new
        {
            message = $"Unlocked {user.Email} and sent a mandatory password-reset email to that address.",
            user = dto
        });
    }

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> SendResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return NotFound(new { message = "User not found." });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = BuildResetUrl(token, user.Id, user.Email);
        var html = await LoadResetEmailAsync(resetUrl);

        try
        {
            await _emailSender.SendEmailAsync(user.Email, "Reset your CosmicStore password", html);
        }
        catch (Exception)
        {
            return StatusCode(503, new { message = "Could not send the reset email right now." });
        }

        return Ok(new { message = $"Password reset email sent to {user.Email}." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.Equals(user.Id, actorId, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "You cannot delete your own account." });
        }

        if (await _userManager.IsInRoleAsync(user, StaticInfo.AdminRole) && await CountAdminsAsync() <= 1)
        {
            return BadRequest(new { message = "You cannot delete the last Admin account." });
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join(" ", result.Errors.Select(error => error.Description)) });
        }

        return NoContent();
    }

    private async Task<AdminUserDto> ToDtoAsync(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
        return new AdminUserDto
        {
            Id = user.Id,
            Email = user.Email,
            UserName = user.UserName,
            Roles = roles.ToList(),
            EmailConfirmed = user.EmailConfirmed,
            IsLocked = AccountLockouts.IsActive(lockoutEnd),
            IsPermanentlyLocked = AccountLockouts.IsPermanent(lockoutEnd)
        };
    }

    private async Task<int> CountAdminsAsync()
    {
        var admins = await _userManager.GetUsersInRoleAsync(StaticInfo.AdminRole);
        return admins.Count;
    }

    private string BuildResetUrl(string token, string userId, string email)
    {
        var baseUrl = StoreUrls.Absolute(_configuration, "ReturnPath:resetPassword", "/account/reset-password");
        return QueryHelpers.AddQueryString(baseUrl, new Dictionary<string, string?>
        {
            ["email"] = email,
            ["userId"] = userId,
            ["token"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))
        });
    }

    private async Task<string> LoadResetEmailAsync(string resetUrl)
    {
        var filePath = Path.Combine(_env.WebRootPath ?? string.Empty, "templates", "ResetPassword.html");
        var htmlTemplate = System.IO.File.Exists(filePath)
            ? await System.IO.File.ReadAllTextAsync(filePath)
            : "<div><a href='{{URL}}'>Reset your password</a></div>";

        return htmlTemplate
            .Replace("{{URL}}", resetUrl)
            .Replace("{{LOGO_URL}}", StoreUrls.LogoUrl(_configuration));
    }
}
