using System.Security.Claims;
using CosmicStoreAPI.Util;
using Data.Interfaces;
using Data.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

/// <summary>
/// Saved checkout addresses for the signed-in user.
/// </summary>
[Authorize]
public class UserAddressesController(IStoreUnitOfWork storeUnitOfWork) : BaseController
{
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;

    /// <summary>
    /// Lists the current user's addresses, default first.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserAddressDto>>> GetAddresses()
    {
        if (GuestPrincipal.IsGuest(User))
        {
            return Ok(Array.Empty<UserAddressDto>());
        }

        var userId = GetUserId();
        var addresses = await _storeUnitOfWork.Repository<UserAddress>()
            .GetAllParams(new PageParams { PageNumber = 1, PageSize = 50 }, a => a.AppUserId == userId);

        return Ok(addresses.OrderByDescending(a => a.IsDefault).ThenBy(a => a.Label).Select(MapToDto));
    }

    /// <summary>
    /// Creates an address. If marked default, clears the previous default.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserAddressDto>> CreateAddress(UserAddressDto dto)
    {
        if (GuestPrincipal.IsGuest(User))
        {
            return StatusCode(403, new { message = "Guest checkout does not include saved addresses." });
        }

        var userId = GetUserId();
        var repo = _storeUnitOfWork.Repository<UserAddress>();

        if (dto.IsDefault)
        {
            await ClearDefaultAsync(userId);
        }

        var address = new UserAddress
        {
            AppUserId = userId,
            Label = dto.Label,
            FullName = dto.FullName,
            StreetAddress = dto.StreetAddress,
            City = dto.City,
            ProvinceOrState = dto.ProvinceOrState,
            CountryCode = dto.CountryCode,
            IsDefault = dto.IsDefault
        };

        repo.Add(address);
        await _storeUnitOfWork.Complete();

        return Ok(MapToDto(address));
    }

    /// <summary>
    /// Updates one of the current user's addresses.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserAddressDto>> UpdateAddress(int id, UserAddressDto dto)
    {
        if (GuestPrincipal.IsGuest(User))
        {
            return StatusCode(403, new { message = "Guest checkout does not include saved addresses." });
        }

        var userId = GetUserId();
        var repo = _storeUnitOfWork.Repository<UserAddress>();
        var address = await repo.GetFirstOrDefault(a => a.Id == id && a.AppUserId == userId);

        if (address is null) return NotFound();

        if (dto.IsDefault)
        {
            await ClearDefaultAsync(userId);
        }

        address.Label = dto.Label;
        address.FullName = dto.FullName;
        address.StreetAddress = dto.StreetAddress;
        address.City = dto.City;
        address.ProvinceOrState = dto.ProvinceOrState;
        address.CountryCode = dto.CountryCode;
        address.IsDefault = dto.IsDefault;

        repo.Update(address);
        await _storeUnitOfWork.Complete();

        return Ok(MapToDto(address));
    }

    /// <summary>
    /// Deletes one of the current user's addresses.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAddress(int id)
    {
        if (GuestPrincipal.IsGuest(User))
        {
            return StatusCode(403, new { message = "Guest checkout does not include saved addresses." });
        }

        var userId = GetUserId();
        var repo = _storeUnitOfWork.Repository<UserAddress>();
        var address = await repo.GetFirstOrDefault(a => a.Id == id && a.AppUserId == userId);

        if (address is null) return NotFound();

        repo.Remove(address);
        await _storeUnitOfWork.Complete();

        return NoContent();
    }

    /// <summary>Reads the signed-in user's id from the JWT; throws if missing.</summary>
    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
    }

    /// <summary>Unsets IsDefault on every other address for this user.</summary>
    private async Task ClearDefaultAsync(string userId)
    {
        var repo = _storeUnitOfWork.Repository<UserAddress>();
        var defaults = await repo.GetAllParams(
            new PageParams { PageNumber = 1, PageSize = 50 },
            a => a.AppUserId == userId && a.IsDefault);

        foreach (var item in defaults)
        {
            item.IsDefault = false;
            repo.Update(item);
        }
    }

    /// <summary>Maps an address entity to the API payload.</summary>
    private static UserAddressDto MapToDto(UserAddress address) => new()
    {
        Id = address.Id,
        Label = address.Label,
        FullName = address.FullName,
        StreetAddress = address.StreetAddress,
        City = address.City,
        ProvinceOrState = address.ProvinceOrState,
        CountryCode = address.CountryCode,
        IsDefault = address.IsDefault
    };
}
