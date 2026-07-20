using System.ComponentModel.DataAnnotations;

namespace Lares.Api.Contracts.Homes;

public record CreateHomeRequest([Required, MaxLength(100)] string Name);

public record JoinHomeRequest([Required, MaxLength(16)] string InviteCode);

public record MemberDto(string UserId, string FullName, string Email, string Role, DateTime JoinedAtUtc);

public record HomeDto(
    Guid Id,
    string Name,
    string Role,
    string? InviteCode,
    IReadOnlyList<MemberDto> Members);

public record RegenerateInviteResponse(string InviteCode);
