using System.Security.Claims;
using Awake.Application.Common.Interfaces;
using Awake.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Awake.Infrastructure.Identity;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public UserRank Rank
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User
                .FindFirstValue("rank");
            return int.TryParse(value, out var rank) ? (UserRank)rank : UserRank.Guest;
        }
    }

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
