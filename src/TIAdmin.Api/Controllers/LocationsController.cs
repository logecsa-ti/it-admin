namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TIAdmin.Api.Filters;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Validators;
using TIAdmin.Domain.Entities;

[ApiController]
[Route("api/v1/locations")]
[Authorize]
[Produces("application/json")]
public sealed class LocationsController : ControllerBase
{
    private readonly IUnitOfWork unitOfWork;

    public LocationsController(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    [HttpGet]
    [Authorize(Policy = TIAdmin.Application.Common.Constants.Permissions.OrganizationView)]
    public async Task<ActionResult<ApiResponse<PagedResult<LocationDto>>>> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var page = await unitOfWork.Locations.SearchAsync(query, isActive, cancellationToken);
        return Ok(ApiResponse<PagedResult<LocationDto>>.Ok(page));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = TIAdmin.Application.Common.Constants.Permissions.OrganizationView)]
    public async Task<ActionResult<ApiResponse<LocationDto>>> GetById(int id, CancellationToken cancellationToken)
    {
        var location = await unitOfWork.Locations.GetByIdAsync(id, cancellationToken);
        if (location is null || location.IsDeleted)
        {
            return NotFound(ApiResponse<LocationDto>.Fail("Ubicación no encontrada."));
        }

        var dto = new LocationDto(location.Id, location.Code, location.Name, location.Address, location.City, location.Country, location.IsActive);
        return Ok(ApiResponse<LocationDto>.Ok(dto));
    }

    [HttpPost]
    [Authorize(Policy = TIAdmin.Application.Common.Constants.Permissions.OrganizationManage)]
    [ValidatableRequest]
    public async Task<ActionResult<ApiResponse<LocationDto>>> Create([FromBody] CreateLocationRequest request, CancellationToken cancellationToken)
    {
        var validator = new CreateLocationRequestValidator();
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<LocationDto>.Fail("Datos inválidos.",
                validation.Errors.Select(e => new ApiError("VALIDATION_ERROR", e.ErrorMessage))));
        }

        if (await unitOfWork.Locations.ExistsCodeAsync(request.Code, null, cancellationToken))
        {
            return BadRequest(ApiResponse<LocationDto>.Fail("El código de la ubicación ya existe."));
        }

        var location = new Location
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Address = request.Address?.Trim(),
            City = request.City?.Trim(),
            Country = request.Country?.Trim(),
            IsActive = true
        };

        await unitOfWork.Locations.AddAsync(location, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new LocationDto(location.Id, location.Code, location.Name, location.Address, location.City, location.Country, location.IsActive);
        return CreatedAtAction(nameof(GetById), new { id = location.Id }, ApiResponse<LocationDto>.Ok(dto));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = TIAdmin.Application.Common.Constants.Permissions.OrganizationManage)]
    [ValidatableRequest]
    public async Task<ActionResult<ApiResponse<LocationDto>>> Update(int id, [FromBody] UpdateLocationRequest request, CancellationToken cancellationToken)
    {
        var validator = new UpdateLocationRequestValidator();
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<LocationDto>.Fail("Datos inválidos.",
                validation.Errors.Select(e => new ApiError("VALIDATION_ERROR", e.ErrorMessage))));
        }

        var location = await unitOfWork.Locations.GetByIdAsync(id, cancellationToken);
        if (location is null || location.IsDeleted)
        {
            return NotFound(ApiResponse<LocationDto>.Fail("Ubicación no encontrada."));
        }

        if (await unitOfWork.Locations.ExistsCodeAsync(request.Code, id, cancellationToken))
        {
            return BadRequest(ApiResponse<LocationDto>.Fail("El código de la ubicación ya existe."));
        }

        location.Code = request.Code.Trim();
        location.Name = request.Name.Trim();
        location.Address = request.Address?.Trim();
        location.City = request.City?.Trim();
        location.Country = request.Country?.Trim();
        location.IsActive = request.IsActive;

        unitOfWork.Locations.Update(location);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new LocationDto(location.Id, location.Code, location.Name, location.Address, location.City, location.Country, location.IsActive);
        return Ok(ApiResponse<LocationDto>.Ok(dto));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = TIAdmin.Application.Common.Constants.Permissions.OrganizationManage)]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken cancellationToken)
    {
        var location = await unitOfWork.Locations.GetByIdAsync(id, cancellationToken);
        if (location is null || location.IsDeleted)
        {
            return NotFound(ApiResponse.Fail("Ubicación no encontrada."));
        }

        unitOfWork.Locations.Delete(location);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse.Ok("Ubicación eliminada."));
    }
}