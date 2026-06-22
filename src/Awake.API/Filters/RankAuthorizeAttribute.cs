using Awake.API.Extensions;
using Awake.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Awake.API.Filters;

public class RankAuthorizeAttribute(UserRank minimumRank) : AuthorizeAttribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var rankClaim = user.FindFirst("rank");
        if (rankClaim is null || !int.TryParse(rankClaim.Value, out var rankValue))
        {
            context.Result = new ForbidResult();
            return;
        }

        var userRank = (UserRank)rankValue;
        if (userRank < minimumRank)
            context.Result = new ForbidResult();
    }
}
