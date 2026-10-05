namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TIAdmin.Api.Filters;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Validators;
using TIAdmin.Domain.Entities;

[ApiController]
[Route("api/v1/departments")]
[Authorize]
[Produces("application/json")]
public sealed class DepartmentsController : ControllerBase
{
    private readonly IUnitOfWork unitOfWork;

    public DepartmentsController(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    [HttpGet]
    [Authorize(Policy = TIAdmin.Application.Common.Constants.Permissions.OrganizationView)]
    public async Task<ActionResult<ApiResponse<PagedResult<DepartmentDto>>>> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] bool? isActive,
        [FromQuery] int? parentId,
        CancellationToken cancellationToken)
    {
        var page = await unitOfWork.Departments.SearchAsync(query, isActive, parentId, cancellationToken);
        return Ok(ApiResponse<PagedResult<DepartmentDto>>.Ok(page));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = TIAdmin.Application.Common.Constants.Permissions.OrganizationView)]
    public async Task<ActionResult<ApiResponse<DepartmentDto>>> GetById(int id, CancellationToken cancellationToken)
    {
        var department = await unitOfWork.Departments.GetByIdAsync(id, cancellationToken);
        if (department is null || department.IsDeleted)
        {
            return NotFound(ApiResponse<DepartmentDto>.Fail("Departamento no encontrado."));
        }

        var dto = new DepartmentDto(department.Id, department.Code, department.Name, department.Description, department.ManagerId, department.ParentId, department.IsActive);
        return Ok(ApiResponse<DepartmentDto>.Ok(dto));
    }

    [HttpPost]
    [Authorize(Policy = TIAdmin.Application.Common.Constants.Permissions.OrganizationManage)]
    [ValidatableRequest]
    public async Task<ActionResult<ApiResponse<DepartmentDto>>> Create([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var validator = new CreateDepartmentRequestValidator();
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<DepartmentDto>.Fail("Datos inválidos.",
                validation.Errors.Select(e => new ApiError("VALIDATION_ERROR", e.ErrorMessage))));
        }

        if (await unitOfWork.Departments.ExistsCodeAsync(request.Code, null, cancellationToken))
        {
            return BadRequest(ApiResponse<DepartmentDto>.Fail("El código del departamento ya existe."));
        }

        if (request.ParentId is { } parentId && !await unitOfWork.Departments.ExistsAsync(parentId, cancellationToken))
        {
            return BadRequest(ApiResponse<DepartmentDto>.Fail("El departamento padre no existe."));
        }

        var department = new Department
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            ManagerId = request.ManagerId,
            ParentId = request.ParentId,
            IsActive = true
        };

        await unitOfWork.Departments.AddAsync(department, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new DepartmentDto(department.Id, department.Code, department.Name, department.Description, department.ManagerId, department.ParentId, department.IsActive);
        return CreatedAtAction(nameof(GetById), new { id = department.Id }, ApiResponse<DepartmentDto>.Ok(dto));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = TIAdmin.Application.Common.Constants.Permissions.OrganizationManage)]
    [ValidatableRequest]
    public async Task<ActionResult<ApiResponse<DepartmentDto>>> Update(int id, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var validator = new UpdateDepartmentRequestValidator();
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<DepartmentDto>.Fail("Datos inválidos.",
                validation.Errors.Select(e => new ApiError("VALIDATION_ERROR", e.ErrorMessage))));
        }

        var department = await unitOfWork.Departments.GetByIdAsync(id, cancellationToken);
        if (department is null || department.IsDeleted)
        {
            return NotFound(ApiResponse<DepartmentDto>.Fail("Departamento no encontrado."));
        }

        if (await unitOfWork.Departments.ExistsCodeAsync(request.Code, id, cancellationToken))
        {
            return BadRequest(ApiResponse<DepartmentDto>.Fail("El código del departamento ya existe."));
        }

        if (request.ParentId is { } parentId)
        {
            if (!await unitOfWork.Departments.ExistsAsync(parentId, cancellationToken))
            {
                return BadRequest(ApiResponse<DepartmentDto>.Fail("El departamento padre no existe."));
            }

            if (await unitOfWork.Departments.WouldCreateCycleAsync(id, parentId, cancellationToken))
            {
                return BadRequest(ApiResponse<DepartmentDto>.Fail(
                    "El departamento padre no puede ser el mismo departamento ni uno de sus subdepartamentos."));
            }
        }

        department.Code = request.Code.Trim();
        department.Name = request.Name.Trim();
        department.Description = request.Description?.Trim();
        department.ManagerId = request.ManagerId;
        department.ParentId = request.ParentId;
        department.IsActive = request.IsActive;

        unitOfWork.Departments.Update(department);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new DepartmentDto(department.Id, department.Code, department.Name, department.Description, department.ManagerId, department.ParentId, department.IsActive);
        return Ok(ApiResponse<DepartmentDto>.Ok(dto));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = TIAdmin.Application.Common.Constants.Permissions.OrganizationManage)]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken cancellationToken)
    {
        var department = await unitOfWork.Departments.GetByIdAsync(id, cancellationToken);
        if (department is null || department.IsDeleted)
        {
            return NotFound(ApiResponse.Fail("Departamento no encontrado."));
        }

        var withChildren = await unitOfWork.Departments.GetWithChildrenAsync(id, cancellationToken);
        if (withChildren?.Children?.Count > 0)
        {
            return Conflict(ApiResponse.Fail("No se puede eliminar un departamento que tiene subdepartamentos."));
        }

        unitOfWork.Departments.Delete(department);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse.Ok("Departamento eliminado."));
    }
}