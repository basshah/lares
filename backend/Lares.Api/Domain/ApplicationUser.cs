using Microsoft.AspNetCore.Identity;

namespace Lares.Api.Domain;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
