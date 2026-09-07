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
        // 1. Check if email exists using _userManager
        // 2. Create new AppUser
        // 3. _userManager.CreateAsync()
        // 4. Return new UserDto with a JWT from _tokenService
        // 1. Check if email is already taken
            
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
                if (user.Email == "NormandJean1@yahoo.com")
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

            //     // ==========================================
            //     // LOAD HTML TEMPLATE FROM WWWROOT
            //     // ==========================================
            //     // Create a folder in wwwroot named 'templates' and add 'ConfirmEmail.html'
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
}