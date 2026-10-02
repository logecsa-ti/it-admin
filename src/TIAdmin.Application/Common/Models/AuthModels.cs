namespace TIAdmin.Application.Common.Models;

public sealed record LoginRequest(string UserName, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    UserProfileResponse User);

public sealed record UserProfileResponse(
    int Id,
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    bool IsActive,
    DateTime? LastLoginAt,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
