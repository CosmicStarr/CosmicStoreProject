using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Models;

namespace CosmicStoreAPI.Controllers;

[Authorize(Roles = "Admin")]
public class AdminUsersController(UserManager<AppUser> userManager) : BaseController
{
    private readonly UserManager<AppUser> _userManager = userManager;

    [HttpGet]
    public ActionResult<IEnumerable<object>> GetUsers()
    {
        var users = _userManager.Users
            .Select(u => new { u.Id, u.Email, u.UserName })
            .ToList();

        return Ok(users);
    }
}
