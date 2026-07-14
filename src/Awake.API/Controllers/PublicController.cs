using Awake.Application.Features.Public.Queries.GetLeaderboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Awake.API.Controllers;

[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController(ISender sender, IMemoryCache cache) : ControllerBase
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    // Публичный топ для лендинга: кэш 5 минут, чтобы не грузить БД
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard(CancellationToken ct)
    {
        var entries = await cache.GetOrCreateAsync("public:leaderboard", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await sender.Send(new GetLeaderboardQuery(), ct);
        });
        return Ok(entries);
    }
}
