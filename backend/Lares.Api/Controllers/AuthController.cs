using System.Security.Claims;
using Lares.Api.Contracts.Auth;
using Lares.Api.Data;
using Lares.Api.Domain;
using Lares.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    LaresDbContext db,
    TokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName.Trim(),
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var code = result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName")
                ? "EMAIL_TAKEN"
                : "WEAK_PASSWORD";
            return BadRequest(new ApiError(code));
        }

        return await IssueTokensAsync(user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new ApiError("INVALID_CREDENTIALS"));

        return await IssueTokensAsync(user);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        var hash = TokenService.HashToken(request.RefreshToken);
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash);

        if (stored is null || !stored.IsActive)
            return Unauthorized(new ApiError("INVALID_REFRESH_TOKEN"));

        // Rotation: the presented token is spent, a fresh one replaces it.
        stored.RevokedAtUtc = DateTime.UtcNow;
        return await IssueTokensAsync(stored.User);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var hash = TokenService.HashToken(request.RefreshToken);
        var stored = await db.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId);

        if (stored is not null && stored.RevokedAtUtc is null)
        {
            stored.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return Unauthorized(new ApiError("UNKNOWN_USER"));

        var roles = await userManager.GetRolesAsync(user);
        return new UserDto(user.Id, user.Email!, user.FullName, roles.ToList());
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, expiresAtUtc) = tokenService.CreateAccessToken(user, roles);

        var rawRefreshToken = TokenService.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = TokenService.HashToken(rawRefreshToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(tokenService.RefreshTokenDays),
        });
        await db.SaveChangesAsync();

        return new AuthResponse(
            accessToken,
            expiresAtUtc,
            rawRefreshToken,
            new UserDto(user.Id, user.Email!, user.FullName, roles.ToList()));
    }
}
