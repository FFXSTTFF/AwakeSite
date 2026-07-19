using Awake.Application.Common.Interfaces;
using Awake.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Awake.API.Controllers;

[ApiController]
[Route("api/items")]
[Authorize]
public class ItemsController(IItemCacheService cache) : ControllerBase
{
    [HttpGet("search")]
    public IActionResult Search(
        [FromQuery] string q = "",
        [FromQuery] string? category = null,
        [FromQuery] string? exclude = null,
        [FromQuery] BoostType? boostType = null)
    {
        if (boostType is not null)
            return Ok(cache.SearchBoosts(q, boostType.Value));

        if (q.Length < 2 && string.IsNullOrEmpty(category))
            return Ok(Array.Empty<object>());

        var results = cache.Search(q, category, exclude);
        return Ok(results);
    }
}
