using System.Security.Claims;
using System.Security.Cryptography;
using Lares.Api.Contracts.Auth;
using Lares.Api.Contracts.Homes;
using Lares.Api.Data;
using Lares.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HomesController(LaresDbContext db, IConfiguration configuration) : ControllerBase
{
    [Authorize]
    [HttpPost("create")]
    public async Task<ActionResult<HomeDto>> Create(CreateHomeRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (await GetMembershipAsync(userId) is not null)
            return BadRequest(new ApiError("ALREADY_IN_A_HOME"));

        var home = new Home
        {
            Name = request.Name.Trim(),
            InviteCode = await GenerateUniqueInviteCodeAsync(),
        };
        db.Homes.Add(home);
        db.Memberships.Add(new Membership { Home = home, UserId = userId, Role = HomeRole.Owner });
        await db.SaveChangesAsync();

        return await BuildHomeDtoAsync(home.Id, userId);
    }

    [Authorize]
    [HttpPost("join")]
    public async Task<ActionResult<HomeDto>> Join(JoinHomeRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (await GetMembershipAsync(userId) is not null)
            return BadRequest(new ApiError("ALREADY_IN_A_HOME"));

        var code = request.InviteCode.Trim().ToUpperInvariant();
        var home = await db.Homes.SingleOrDefaultAsync(h => h.InviteCode == code);
        if (home is null)
            return BadRequest(new ApiError("INVALID_INVITE_CODE"));

        db.Memberships.Add(new Membership { HomeId = home.Id, UserId = userId, Role = HomeRole.Member });
        await db.SaveChangesAsync();

        return await BuildHomeDtoAsync(home.Id, userId);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<HomeDto>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        return await BuildHomeDtoAsync(membership.HomeId, userId);
    }

    [Authorize]
    [HttpPost("regenerate-invite")]
    public async Task<ActionResult<RegenerateInviteResponse>> RegenerateInvite()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));
        if (membership.Role != HomeRole.Owner)
            return StatusCode(403, new ApiError("NOT_HOME_OWNER"));

        membership.Home.InviteCode = await GenerateUniqueInviteCodeAsync();
        await db.SaveChangesAsync();

        return new RegenerateInviteResponse(membership.Home.InviteCode);
    }

    [Authorize]
    [HttpPost("leave")]
    public async Task<IActionResult> Leave()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));
        if (membership.Role == HomeRole.Owner)
            return BadRequest(new ApiError("OWNER_CANNOT_LEAVE"));

        db.Memberships.Remove(membership);
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Task<Membership?> GetMembershipAsync(string userId) =>
        db.Memberships.Include(m => m.Home).SingleOrDefaultAsync(m => m.UserId == userId);

    private async Task<HomeDto> BuildHomeDtoAsync(Guid homeId, string callerUserId)
    {
        var memberships = await db.Memberships
            .Include(m => m.User)
            .Include(m => m.Home)
            .Where(m => m.HomeId == homeId)
            .ToListAsync();

        var ordered = memberships.OrderBy(m => m.Role).ThenBy(m => m.JoinedAtUtc).ToList();
        var caller = ordered.Single(m => m.UserId == callerUserId);
        var home = caller.Home;

        var members = ordered
            .Select(m => new MemberDto(m.UserId, m.User.FullName, m.User.Email!, m.Role.ToString(), m.JoinedAtUtc))
            .ToList();

        return new HomeDto(
            home.Id,
            home.Name,
            caller.Role.ToString(),
            caller.Role == HomeRole.Owner ? home.InviteCode : null,
            members);
    }

    private static string GenerateInviteCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(length);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    private async Task<string> GenerateUniqueInviteCodeAsync()
    {
        var length = configuration.GetValue<int>("Home:InviteCodeLength");
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = GenerateInviteCode(length);
            if (!await db.Homes.AnyAsync(h => h.InviteCode == code))
                return code;
        }

        throw new InvalidOperationException("Could not generate a unique invite code.");
    }
}
