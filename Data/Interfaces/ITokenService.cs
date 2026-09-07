using Models;

namespace Data.Interfaces;


public interface ITokenService
{
    Task<string> CreateToken(AppUser appUser);
}