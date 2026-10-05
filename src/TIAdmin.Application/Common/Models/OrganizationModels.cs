namespace TIAdmin.Application.Common.Models;

public record DepartmentDto(int Id, string Code, string Name, string? Description, int? ManagerId, int? ParentId, bool IsActive);

public record LocationDto(int Id, string Code, string Name, string? Address, string? City, string? Country, bool IsActive);

public record CreateDepartmentRequest(string Code, string Name, string? Description, int? ManagerId, int? ParentId);

public record UpdateDepartmentRequest(string Code, string Name, string? Description, int? ManagerId, int? ParentId, bool IsActive);

public record CreateLocationRequest(string Code, string Name, string? Address, string? City, string? Country);

public record UpdateLocationRequest(string Code, string Name, string? Address, string? City, string? Country, bool IsActive);
