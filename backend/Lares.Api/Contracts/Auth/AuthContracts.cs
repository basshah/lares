using System.ComponentModel.DataAnnotations;

namespace Lares.Api.Contracts.Auth;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required, MaxLength(100)] string FullName);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RefreshRequest([Required] string RefreshToken);

public record UserDto(string Id, string Email, string FullName, IReadOnlyList<string> Roles);

public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    UserDto User);

/// <summary>Machine-readable error payload; the frontend translates <see cref="Code"/> via i18n.</summary>
public record ApiError(string Code);
