using System.Security.Claims;
using Data.Interfaces;
using Data.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using Models.AngularDTOs;

namespace CosmicStoreAPI.Controllers;

[Authorize]
public class UserAddressesController(IStoreUnitOfWork storeUnitOfWork) : BaseController
{
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserAddressDto>>> GetAddresses()
    {
        var userId = GetUserId();
        var addresses = await _storeUnitOfWork.Repository<UserAddress>()
            .GetAllParams(new PageParams { PageNumber = 1, PageSize = 50 }, a => a.AppUserId == userId);

        return Ok(addresses.OrderByDescending(a => a.IsDefault).ThenBy(a => a.Label).Select(MapToDto));
    }

    [HttpPost]
    public async Task<ActionResult<UserAddressDto>> CreateAddress(UserAddressDto dto)
    {
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

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserAddressDto>> UpdateAddress(int id, UserAddressDto dto)
    {
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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAddress(int id)
    {
        var userId = GetUserId();
        var repo = _storeUnitOfWork.Repository<UserAddress>();
        var address = await repo.GetFirstOrDefault(a => a.Id == id && a.AppUserId == userId);

        if (address is null) return NotFound();

        repo.Remove(address);
        await _storeUnitOfWork.Complete();

        return NoContent();
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
    }

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
