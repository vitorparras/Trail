using Microsoft.AspNetCore.Identity;

namespace Trail.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public bool IsAdmin { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}
