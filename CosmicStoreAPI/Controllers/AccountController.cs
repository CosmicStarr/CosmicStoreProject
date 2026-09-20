namespace CosmicStoreAPI.Controllers;

using System.Security.Claims;
using System.Text;
using CosmicStoreAPI.Error;
using CosmicStoreAPI.Util;
using Data.Interfaces;
using Data.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Models;
using Models.AngularDTOs;


/// <summary>
/// Identity: register, login, current-user JWT, email confirm, password reset, and profile.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly IEmailChangeService _emailChangeService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly RoleManager<IdentityRole> _roleManager;

    // Inject Microsoft's built-in managers directly
    public AccountController(
        UserManager<AppUser> userManager, 
        SignInManager<AppUser> signInManager, 
        ITokenService tokenService,IEmailSender emailSender,
        IEmailChangeService emailChangeService,
        IConfiguration configuration, 
        IWebHostEnvironment env, 
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _emailChangeService = emailChangeService;
        _configuration = configuration; 
        _env = env;
        _roleManager = roleManager;
    }

    /// <summary>
    /// Creates an Identity user, emails a confirmation link, and returns a JWT. A hardcoded admin email is granted the Admin role.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<RegisterDto>> Register(RegisterDto registerDto)
    {
            //1.Find Current User
            if (await _userManager.FindByEmailAsync(registerDto.Email) != null)
            {
                return BadRequest(new { message = "Email is already in use" });
            }

            var userName = registerDto.UserName.Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                return BadRequest(new { message = "Username is required." });
            }

            if (await _userManager.FindByNameAsync(userName) != null)
            {
                return BadRequest(new { message = "Username is already in use" });
            }

            // 2. Create the AppUser instance
            var user = new AppUser
            {
                UserName = userName,
                Email = registerDto.Email
            };

            // 3. Save user via UserManager (handles hashing the password securely)
            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (result.Succeeded)
            {
                if (user.Email == "NormandJ85@outlook.com")
                {         
                    await _roleManager.CreateAsync(new IdentityRole(StaticInfo.AdminRole));
                    await _userManager.AddToRolesAsync(user, new[] { StaticInfo.AdminRole });
                    var claim = new Claim("JobDepartment", StaticInfo.Job);
                    await _userManager.AddClaimAsync(user, claim);
                }

                user = await _userManager.FindByEmailAsync(user.Email);
                var tokenToGenerate = await _userManager.GenerateEmailConfirmationTokenAsync(user!);
                
                var urlMessage = BuildReturnUrl(
                    "ReturnPath:confirmEmail",
                    tokenToGenerate,
                    user!.Id,
                    user.Email!);

             // ==========================================
             // LOAD HTML TEMPLATE FROM WWWROOT
             // ==========================================
             // Create a folder in wwwroot named 'templates' and add 'ConfirmEmail.html'
                var filePath = Path.Combine(_env.WebRootPath, "templates", "ConfirmEmail.html");
                
                string htmlTemplate = string.Empty;
                if (System.IO.File.Exists(filePath)) 
                {
                    htmlTemplate = await System.IO.File.ReadAllTextAsync(filePath);
                }
                else
                {
                    // Fallback inline template if file is missing so app doesn't crash
                    htmlTemplate = "<div>Please confirm your email: <a href='{{URL}}'>Confirm</a></div>";
                }

                // Replace placeholders in your HTML file
                string text = htmlTemplate.Replace("{{URL}}", urlMessage);

                try
                {
                    await _emailSender.SendEmailAsync(user.Email!, "Confirm your email!", text);
                }
                catch (Exception)
                {
                    // The account already exists; do not fail signup because confirmation mail could not send.
                }
            }
            else
            {
                return BadRequest(new ApiValidationResponse(result.Errors.Select(error => error.Description)));
            }

        // 4. Return the token and user details
#pragma warning disable CS8601 // Possible null reference assignment.
        return new RegisterDto
            {
                Email = user.Email,
                Token = await _tokenService.CreateToken(user),
                UserName = user.UserName ?? userName,
                EmailConfirmed = user.EmailConfirmed,
                IsGuest = false
            };
#pragma warning restore CS8601 // Possible null reference assignment.
    }

    /// <summary>
    /// Validates email/password and returns a JWT for the Angular client.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
    {
        // 1. Find user by email using _userManager
        // 2. Check password using _signInManager.CheckPasswordSignInAsync()
        // 3. If successful, return UserDto with a JWT from _tokenService
        var user = await _userManager.FindByEmailAsync(loginDto.Email);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "No account found for this email.",
                    code = "accountNotFound"
                });
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                return Unauthorized(new { message = "This account is locked. Contact support if you need help recovering it." });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded) return Unauthorized(new { message = "Invalid email or password" });

            return await ToUserDto(user);
    }

    /// <summary>
    /// Issues a guest checkout JWT. No Identity user is created or stored.
    /// </summary>
    [HttpPost("guest")]
    public ActionResult<UserDto> ContinueAsGuest()
    {
        var guestId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!GuestPrincipal.IsGuest(User) || string.IsNullOrWhiteSpace(guestId))
        {
            guestId = Guid.NewGuid().ToString();
        }

        return GuestUserDto(guestId);
    }

    /// <summary>
    /// Returns the signed-in user and a fresh JWT (used on app load / token refresh).
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        if (GuestPrincipal.IsGuest(User))
        {
            var guestId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.NewGuid().ToString();
            return GuestUserDto(guestId);
        }

        var user = await GetSignedInUserAsync();
        if (user == null) return Unauthorized();

        return await ToUserDto(user);
    }

    /// <summary>
    /// Confirms the registration email using the token from the confirmation link.
    /// </summary>
    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailDto dto)
    {
        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user == null)
        {
            return BadRequest(new { message = "Invalid confirmation link." });
        }

        var confirmed = false;
        foreach (var candidate in IdentityTokenCandidates(dto.Token))
        {
            var result = await _userManager.ConfirmEmailAsync(user, candidate);
            if (result.Succeeded)
            {
                confirmed = true;
                break;
            }
        }

        if (!confirmed)
        {
            return BadRequest(new { message = "Email confirmation failed. The link may have expired." });
        }

        return Ok(new { message = "Email confirmed successfully. You can now sign in." });
    }

    /// <summary>
    /// Sends a password-reset email if the address exists. Always returns the same message to avoid leaking accounts.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        string? resetUrl = null;
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user != null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            resetUrl = BuildReturnUrl("ReturnPath:resetPassword", token, user.Id, user.Email!);
            var html = await LoadEmailTemplateAsync("ResetPassword.html", resetUrl);
            try
            {
                await _emailSender.SendEmailAsync(user.Email!, "Reset your CosmicStore password", html);
            }
            catch (Exception)
            {
                // Same generic response whether mail sent or not.
            }
        }

        return Ok(new { message = "If that email exists, a reset link was sent." });
    }

    /// <summary>
    /// Checks that a password-reset link is still valid before the user chooses a new password.
    /// </summary>
    [HttpPost("verify-reset-password")]
    public async Task<IActionResult> VerifyResetPassword(VerifyResetPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            return BadRequest(new { message = "This reset link is invalid or has expired." });
        }

        if (await ResolvePasswordResetTokenAsync(user, dto.Token) is null)
        {
            return BadRequest(new { message = "This reset link is invalid or has expired." });
        }

        return Ok(new { message = "Reset link verified. Choose a new password." });
    }

    /// <summary>
    /// Sets a new password from the reset-link token.
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            return BadRequest(new { message = "Invalid reset request." });
        }

        var token = await ResolvePasswordResetTokenAsync(user, dto.Token);
        if (token is null)
        {
            return BadRequest(new { message = "This reset link is invalid or has expired." });
        }

        var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Password reset failed.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        return Ok(new { message = "Password updated successfully. You can now sign in." });
    }

    /// <summary>
    /// Updates the signed-in user's display name and returns a new JWT.
    /// </summary>
    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<UserDto>> UpdateProfile(UpdateProfileDto dto)
    {
        if (GuestPrincipal.IsGuest(User))
        {
            return StatusCode(403, new { message = "Guest checkout does not include a profile." });
        }

        var user = await GetSignedInUserAsync();
        if (user == null) return Unauthorized();

        user.UserName = dto.UserName;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Could not update profile.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        return await ToUserDto(user);
    }

    /// <summary>
    /// Changes the signed-in user's password after verifying the current one.
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        if (GuestPrincipal.IsGuest(User))
        {
            return StatusCode(403, new { message = "Guest checkout does not include a profile." });
        }

        var user = await GetSignedInUserAsync();
        if (user == null) return Unauthorized();

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Could not change password.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        return Ok(new { message = "Password changed successfully." });
    }

    /// <summary>
    /// Confirms the current password and issues a short-lived token required to reveal/submit an email change.
    /// </summary>
    [Authorize]
    [HttpPost("reauthenticate")]
    public async Task<IActionResult> Reauthenticate(ReauthenticateDto dto)
    {
        if (GuestPrincipal.IsGuest(User))
        {
            return StatusCode(403, new { message = "Guest checkout does not include a profile." });
        }

        var signedIn = await GetSignedInUserAsync();
        if (signedIn == null) return Unauthorized();

        if (await _userManager.IsLockedOutAsync(signedIn))
        {
            return Unauthorized(new { message = "This account is locked." });
        }

        var passwordCheck = await _signInManager.CheckPasswordSignInAsync(signedIn, dto.CurrentPassword, false);
        if (!passwordCheck.Succeeded)
        {
            return Unauthorized(new { message = "Current password is incorrect." });
        }

        var reauthToken = await _emailChangeService.IssueReauthTokenAsync(signedIn.Id);
        return Ok(new
        {
            reauthToken,
            expiresInSeconds = (int)_emailChangeService.ReauthLifetime.TotalSeconds
        });
    }

    /// <summary>
    /// Stores a pending email and sends a short-lived verification link to the new address,
    /// plus a lock-account alert to the current address. The active email is not changed yet.
    /// </summary>
    [Authorize]
    [HttpPost("change-email")]
    public async Task<IActionResult> RequestEmailChange(RequestEmailChangeDto dto)
    {
        if (GuestPrincipal.IsGuest(User))
        {
            return StatusCode(403, new { message = "Guest checkout does not include a profile." });
        }

        var signedIn = await GetSignedInUserAsync();
        if (signedIn == null) return Unauthorized();

        var newEmail = dto.NewEmail.Trim();
        var currentEmail = signedIn.Email ?? string.Empty;
        if (string.Equals(_userManager.NormalizeEmail(newEmail), _userManager.NormalizeEmail(currentEmail), StringComparison.Ordinal))
        {
            return BadRequest(new { message = "That is already your email address." });
        }

        if (await _userManager.FindByEmailAsync(newEmail) != null)
        {
            return BadRequest(new { message = "This email cannot be used." });
        }

        if (!await _emailChangeService.ConsumeReauthTokenAsync(signedIn.Id, dto.ReauthToken))
        {
            return Unauthorized(new { message = "Please confirm your password again before changing your email." });
        }

        await _emailChangeService.StorePendingChangeAsync(signedIn.Id, currentEmail, newEmail);

        var changeToken = await _userManager.GenerateChangeEmailTokenAsync(signedIn, newEmail);
        var confirmUrl = BuildAppUrl("ReturnPath:confirmEmailChange", new Dictionary<string, string?>
        {
            ["email"] = newEmail,
            ["userId"] = signedIn.Id,
            ["token"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(changeToken))
        });

        var lockToken = await _emailChangeService.IssueLockTokenAsync(signedIn.Id);
        var lockUrl = BuildAppUrl("ReturnPath:lockAccount", new Dictionary<string, string?>
        {
            ["userId"] = signedIn.Id,
            ["token"] = lockToken
        });

        var confirmHtml = await LoadEmailTemplateAsync("ConfirmEmailChange.html", confirmUrl);
        try
        {
            await _emailSender.SendEmailAsync(newEmail, "Confirm your new CosmicStore email", confirmHtml);
        }
        catch (Exception)
        {
            await _emailChangeService.RemovePendingChangeAsync(signedIn.Id);
            await _emailChangeService.RemoveLockTokenAsync(signedIn.Id);
            return StatusCode(503, new { message = "We could not send a verification email. Try again in a few minutes." });
        }

        var alertHtml = await LoadEmailTemplateAsync("EmailChangeAlert.html", lockUrl);
        try
        {
            await _emailSender.SendEmailAsync(currentEmail, "Your CosmicStore email was recently changed", alertHtml);
        }
        catch (Exception)
        {
            // The new-address verification already went out; do not fail the request if the alert cannot send.
        }

        return Ok(new
        {
            message = $"A verification link was sent to {newEmail}. It expires in 20 minutes.",
            pendingEmail = newEmail,
            expiresInMinutes = (int)_emailChangeService.PendingChangeLifetime.TotalMinutes
        });
    }

    /// <summary>
    /// Applies a pending email after the new address clicks the short-lived verification link.
    /// </summary>
    [HttpPost("confirm-change-email")]
    public async Task<IActionResult> ConfirmEmailChange(ConfirmEmailChangeDto dto)
    {
        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user == null)
        {
            return BadRequest(new { message = "This verification link is invalid or has expired." });
        }

        var pending = await _emailChangeService.GetPendingChangeAsync(user.Id);
        if (pending == null
            || !string.Equals(
                _userManager.NormalizeEmail(pending.NewEmail),
                _userManager.NormalizeEmail(dto.Email),
                StringComparison.Ordinal))
        {
            return BadRequest(new { message = "This verification link is invalid or has expired." });
        }

        IdentityResult? changeResult = null;
        foreach (var candidate in IdentityTokenCandidates(dto.Token))
        {
            changeResult = await _userManager.ChangeEmailAsync(user, pending.NewEmail, candidate);
            if (changeResult.Succeeded)
            {
                break;
            }
        }

        if (changeResult is not { Succeeded: true })
        {
            return BadRequest(new { message = "This verification link is invalid or has expired." });
        }

        if (string.Equals(user.UserName, pending.OldEmail, StringComparison.OrdinalIgnoreCase))
        {
            user.UserName = pending.NewEmail;
            await _userManager.UpdateAsync(user);
        }

        await _emailChangeService.RemovePendingChangeAsync(user.Id);
        return Ok(new { message = "Your email address has been updated. Sign in with the new address." });
    }

    /// <summary>
    /// Locks the account from the security alert sent to the original email address.
    /// </summary>
    [HttpPost("lock-account")]
    public async Task<IActionResult> LockAccount(LockAccountDto dto)
    {
        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user == null || !await _emailChangeService.ValidateLockTokenAsync(user.Id, dto.Token))
        {
            return BadRequest(new { message = "This lock link is invalid or has expired." });
        }

        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        await _userManager.UpdateSecurityStampAsync(user);
        await _emailChangeService.RemovePendingChangeAsync(user.Id);
        await _emailChangeService.RemoveLockTokenAsync(user.Id);

        return Ok(new { message = "Your account has been locked. Sign-in is disabled. Contact support if you need help recovering it." });
    }

    /// <summary>Builds an Angular return URL with a URL-safe Identity token plus userId and email.</summary>
    private string BuildReturnUrl(string configKey, string token, string userId, string email)
    {
        return BuildAppUrl(configKey, new Dictionary<string, string?>
        {
            ["email"] = email,
            ["userId"] = userId,
            ["token"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))
        });
    }

    private string BuildAppUrl(string configKey, Dictionary<string, string?> query)
    {
        var baseUrl = _configuration[configKey];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException($"{configKey} is not configured.");
        }

        return QueryHelpers.AddQueryString(baseUrl, query);
    }

    private async Task<AppUser?> GetSignedInUserAsync()
    {
        if (GuestPrincipal.IsGuest(User))
        {
            return null;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await _userManager.FindByIdAsync(userId);
    }

    /// <summary>
    /// Accepts the URL-safe encoded token from the email link, or a raw Identity token.
    /// </summary>
    private async Task<string?> ResolvePasswordResetTokenAsync(AppUser user, string token)
    {
        foreach (var candidate in IdentityTokenCandidates(token))
        {
            var valid = await _userManager.VerifyUserTokenAsync(
                user,
                _userManager.Options.Tokens.PasswordResetTokenProvider,
                UserManager<AppUser>.ResetPasswordTokenPurpose,
                candidate);

            if (valid)
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> IdentityTokenCandidates(string token)
    {
        var normalized = token.Replace(' ', '+');
        var decoded = DecodeIdentityToken(normalized);
        yield return decoded;

        if (!string.Equals(normalized, decoded, StringComparison.Ordinal))
        {
            yield return normalized;
        }
    }

    private static string DecodeIdentityToken(string token)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return token;
        }
    }

    /// <summary>Loads an HTML email template from wwwroot/templates and injects the action URL.</summary>
    private async Task<string> LoadEmailTemplateAsync(string fileName, string url)
    {
        var filePath = Path.Combine(_env.WebRootPath, "templates", fileName);
        var htmlTemplate = System.IO.File.Exists(filePath)
            ? await System.IO.File.ReadAllTextAsync(filePath)
            : "<div><a href='{{URL}}'>Continue</a></div>";

        return htmlTemplate.Replace("{{URL}}", url);
    }

    private UserDto GuestUserDto(string guestId)
    {
        return new UserDto
        {
            Email = string.Empty,
            Token = _tokenService.CreateGuestToken(guestId),
            UserName = "Guest",
            EmailConfirmed = true,
            IsGuest = true
        };
    }

    private async Task<UserDto> ToUserDto(AppUser user)
    {
        var pending = await _emailChangeService.GetPendingChangeAsync(user.Id);
        return new UserDto
        {
            Email = user.Email!,
            Token = await _tokenService.CreateToken(user),
            UserName = user.UserName ?? "User",
            EmailConfirmed = user.EmailConfirmed,
            IsGuest = false,
            PendingEmail = pending?.NewEmail
        };
    }
}