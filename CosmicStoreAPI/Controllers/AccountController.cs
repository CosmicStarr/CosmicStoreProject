namespace CosmicStoreAPI.Controllers;

using System.Security.Claims;
using System.Web;
using Data.Interfaces;
using Data.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;


[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly RoleManager<IdentityRole> _roleManager;

    // Inject Microsoft's built-in managers directly
    public AccountController(
        UserManager<AppUser> userManager, 
        SignInManager<AppUser> signInManager, 
        ITokenService tokenService,IEmailSender emailSender, 
        IConfiguration configuration, 
        IWebHostEnvironment env, 
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _configuration = configuration; 
        _env = env;
        _roleManager = roleManager;
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterDto>> Register(RegisterDto registerDto)
    {
            //1.Find Current User
            if (await _userManager.FindByEmailAsync(registerDto.Email) != null)
            {
                return BadRequest(new { message = "Email is already in use" });
            }

            // 2. Create the AppUser instance
            var user = new AppUser
            {
                UserName = registerDto.Email,
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
                
                var confirmEmailUrl = new UriBuilder(_configuration["ReturnPath:confirmEmail"]!);
                var uriQuery = HttpUtility.ParseQueryString(confirmEmailUrl.Query);
                uriQuery["token"] = tokenToGenerate;
                uriQuery["userId"] = user!.Id;
                confirmEmailUrl.Query = uriQuery.ToString();
                var urlMessage = confirmEmailUrl.ToString();

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

                await _emailSender.SendEmailAsync(user.Email!, "Confirm your email!", text);
            }
            else
            {
                    var registerDTOErrorList = new RegisterDto();
                    var badInfo = registerDTOErrorList.RegisterErrors = new List<IdentityError>();
                    foreach(var item in result.Errors)
                    {
                        var errors = new IdentityError
                        {
                            Code = item.Code,
                            Description = item.Description
                        };
                        badInfo.Add(errors);
                    }
                    return registerDTOErrorList;
            }

        // 4. Return the token and user details
#pragma warning disable CS8601 // Possible null reference assignment.
        return new RegisterDto
            {
                Email = user.Email,
                Token = await _tokenService.CreateToken(user),
                UserName = registerDto.UserName ?? "User" // Fallback if userName isn't provided
            };
#pragma warning restore CS8601 // Possible null reference assignment.
    }

    [HttpPost("login")]
    public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
    {
        // 1. Find user by email using _userManager
        // 2. Check password using _signInManager.CheckPasswordSignInAsync()
        // 3. If successful, return UserDto with a JWT from _tokenService
        var user = await _userManager.FindByEmailAsync(loginDto.Email);

            if (user == null) return Unauthorized(new { message = "Invalid email or password" });

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded) return Unauthorized(new { message = "Invalid email or password" });

            return new UserDto
            {
                Email = user.Email!,
                Token = await _tokenService.CreateToken(user),
                UserName = user.UserName ?? "User" // Fallback if UserName isn't a direct property on IdentityUser
            };
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var user = await _userManager.FindByEmailAsync(email!);

        if (user == null) return Unauthorized();

        return new UserDto
        {
            Email = user.Email!,
            Token = await _tokenService.CreateToken(user),
            UserName = user.UserName ?? "User"
        };
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailDto dto)
    {
        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user == null)
        {
            return BadRequest(new { message = "Invalid confirmation link." });
        }

        var result = await _userManager.ConfirmEmailAsync(user, dto.Token);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Email confirmation failed. The link may have expired." });
        }

        return Ok(new { message = "Email confirmed successfully. You can now sign in." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user != null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetUrl = BuildReturnUrl("ReturnPath:resetPassword", token, user.Id, user.Email!);
            var html = await LoadEmailTemplateAsync("ResetPassword.html", resetUrl);
            await _emailSender.SendEmailAsync(user.Email!, "Reset your CosmicStore password", html);
        }

        return Ok(new { message = "If that email exists, a reset link was sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            return BadRequest(new { message = "Invalid reset request." });
        }

        var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
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

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<UserDto>> UpdateProfile(UpdateProfileDto dto)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var user = await _userManager.FindByEmailAsync(email!);
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

        return new UserDto
        {
            Email = user.Email!,
            Token = await _tokenService.CreateToken(user),
            UserName = user.UserName ?? "User"
        };
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var user = await _userManager.FindByEmailAsync(email!);
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

    private string BuildReturnUrl(string configKey, string token, string userId, string email)
    {
        var urlBuilder = new UriBuilder(_configuration[configKey]!);
        var query = HttpUtility.ParseQueryString(urlBuilder.Query);
        query["token"] = token;
        query["userId"] = userId;
        query["email"] = email;
        urlBuilder.Query = query.ToString();
        return urlBuilder.ToString();
    }

    private async Task<string> LoadEmailTemplateAsync(string fileName, string url)
    {
        var filePath = Path.Combine(_env.WebRootPath, "templates", fileName);
        var htmlTemplate = System.IO.File.Exists(filePath)
            ? await System.IO.File.ReadAllTextAsync(filePath)
            : "<div><a href='{{URL}}'>Continue</a></div>";

        return htmlTemplate.Replace("{{URL}}", url);
    }
}