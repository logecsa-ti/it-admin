namespace TIAdmin.Application.Common.Models;

/// <summary>
/// Envoltura estándar de respuesta de la API (SPECS.md sección 13).
/// </summary>
public class ApiResponse
{
    protected ApiResponse(bool success, string? message, IEnumerable<ApiError> errors)
    {
        Success = success;
        Message = message;
        Errors = errors?.ToArray() ?? [];
    }

    public bool Success { get; init; }

    public string? Message { get; init; }

    public IReadOnlyCollection<ApiError> Errors { get; init; }

    public static ApiResponse Ok(string? message = null) =>
        new(true, message, []);

    public static ApiResponse Fail(string message, IEnumerable<ApiError>? errors = null) =>
        new(false, message, errors ?? []);
}

public sealed class ApiResponse<TData> : ApiResponse
{
    private ApiResponse(bool success, TData? data, string? message, IEnumerable<ApiError> errors)
        : base(success, message, errors)
    {
        Data = data;
    }

    public TData? Data { get; init; }

    public static ApiResponse<TData> Ok(TData data, string? message = null) =>
        new(true, data, message, []);

    public static new ApiResponse<TData> Fail(string message, IEnumerable<ApiError>? errors = null) =>
        new(false, default, message, errors ?? []);
}

public sealed record ApiError(string Code, string Message);
