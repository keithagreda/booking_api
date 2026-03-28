using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using booking_api.Models;

namespace booking_api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID claim not found.");

        return Guid.Parse(sub);
    }

    public static Role GetUserRole(this ClaimsPrincipal principal)
    {
        var role = principal.FindFirstValue(ClaimTypes.Role)
            ?? throw new UnauthorizedAccessException("Role claim not found.");

        return Enum.Parse<Role>(role);
    }

    public static string GetUserEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? principal.FindFirstValue(ClaimTypes.Email)
            ?? throw new UnauthorizedAccessException("Email claim not found.");
    }
}
