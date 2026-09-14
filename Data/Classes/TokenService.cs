using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Data.Util;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Models;

namespace Data.Interfaces;


/// <summary>
/// Builds the JWT Angular stores after register, login, and profile updates.
/// </summary>
public class TokenService : ITokenService
{
    private readonly IOptions<TokenSettings> _config;
    private readonly UserManager<AppUser> _userManager;

    public TokenService(IOptions<TokenSettings> config, UserManager<AppUser> userManager)
    {
        _config = config;
        _userManager = userManager;
    }

    /// <summary>
    /// Issues a signed JWT with user id, email, Identity claims, and role claims.
    /// </summary>
    public async Task<string> CreateToken(AppUser appUser)
    {
        var claimsList = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, appUser.Id),
            new Claim(ClaimTypes.Email, appUser.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var userClaims = await _userManager.GetClaimsAsync(appUser);
        claimsList.AddRange(userClaims);

        var roles = await _userManager.GetRolesAsync(appUser);
        claimsList.AddRange(roles.Select(role => new Claim("role", role)));

        return WriteToken(claimsList);
    }

    /// <summary>
    /// Issues a checkout JWT that is not backed by an Identity user.
    /// </summary>
    public string CreateGuestToken(string guestId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, guestId),
            new Claim(ClaimTypes.Name, "Guest"),
            new Claim(ClaimTypes.Role, StaticInfo.GuestRole),
            new Claim("role", StaticInfo.GuestRole),
            new Claim("guest", "true"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        return WriteToken(claims);
    }

    private string WriteToken(IEnumerable<Claim> claims)
    {
        var authKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.Value.SecretKey));
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(10),
            Issuer = _config.Value.ValidIssuer,
            Audience = _config.Value.ValidAudience,
            SigningCredentials = new SigningCredentials(authKey, SecurityAlgorithms.HmacSha512Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }
}