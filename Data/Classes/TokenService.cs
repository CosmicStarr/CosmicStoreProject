using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Data.Util;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Models;

namespace Data.Interfaces;


public class TokenService : ITokenService
{
    private readonly IOptions<TokenSettings> _config;
    private readonly UserManager<AppUser> _userManager;

    public TokenService(IOptions<TokenSettings> config, UserManager<AppUser> userManager)
    {
        _config = config;
        _userManager = userManager;
    }

    public async Task<string> CreateToken(AppUser appUser)
    {
        // 1. Standardize claims
        var claimsList = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, appUser.Id), // Standard for User ID
            new Claim(ClaimTypes.Email, appUser.Email ?? string.Empty), // Standard for Email
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // 2. Fetch and append User Claims safely
        var userClaims = await _userManager.GetClaimsAsync(appUser);
        claimsList.AddRange(userClaims);

        // 3. Fetch Roles and append using a clean string literal
        var roles = await _userManager.GetRolesAsync(appUser);
        claimsList.AddRange(roles.Select(role => new Claim("role", role)));

        // 4. Create credentials
        var authKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.Value.SecretKey));
        var creds = new SigningCredentials(authKey, SecurityAlgorithms.HmacSha512Signature);

        // 5. Build the token using UTC time
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claimsList),
            Expires = DateTime.UtcNow.AddDays(10),
            Issuer = _config.Value.ValidIssuer,
            Audience = _config.Value.ValidAudience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}