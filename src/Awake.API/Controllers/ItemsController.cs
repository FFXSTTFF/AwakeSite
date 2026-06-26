using Awake.Application.Common.Interfaces;
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
        [FromQuery] string? exclude = null)
    {
        if (q.Length < 2 && string.IsNullOrEmpty(category))
            return Ok(Array.Empty<object>());

        var results = cache.Search(q, category, exclude);
        return Ok(results);
    }
}
