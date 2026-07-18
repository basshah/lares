using Lares.Api.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Lares.Api.Data;

public class LaresDbContext(DbContextOptions<LaresDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
}
