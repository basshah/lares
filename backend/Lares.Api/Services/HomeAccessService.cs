using Lares.Api.Data;
using Lares.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Lares.Api.Services;

public class HomeAccessService(LaresDbContext db)
{
    public Task<Membership?> GetMembershipAsync(string userId) =>
        db.Memberships.Include(m => m.Home).SingleOrDefaultAsync(m => m.UserId == userId);
}
