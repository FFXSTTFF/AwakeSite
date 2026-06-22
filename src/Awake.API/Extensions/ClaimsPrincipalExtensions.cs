using Awake.Domain.Enums;
using System.Security.Claims;

namespace Awake.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static UserRank GetRank(this ClaimsPrincipal user)
        => (UserRank)int.Parse(user.FindFirstValue("rank")!);
}
