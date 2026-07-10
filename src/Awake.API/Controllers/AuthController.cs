using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Features.Auth.Commands.DiscordLogin;
using Awake.Application.Features.Auth.Commands.Refresh;
using Awake.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Awake.API.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(
    ISender sender,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepository,
    IDiscordOAuthService discordOAuth,
    IConfiguration configuration
) : ControllerBase
{
    private const string StateCookie = "discord_oauth_state";

    [HttpGet("discord/login")]
    public IActionResult DiscordLogin()
    {
        var state = Guid.NewGuid().ToString("N");

        // SameSite=Lax: кука должна выжить top-level redirect discord.com → наш callback
        Response.Cookies.Append(StateCookie, state, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth/discord",
            MaxAge = TimeSpan.FromMinutes(10)
        });

        return Redirect(discordOAuth.GetAuthorizationUrl(state));
    }

    [HttpGet("discord/callback")]
    public async Task<IActionResult> DiscordCallback(
        [FromQuery] string? code, [FromQuery] string? state, CancellationToken ct)
    {
        var frontendUrl = configuration["Cors:AllowedOrigin"] ?? "";

        var expectedState = Request.Cookies[StateCookie];
        Response.Cookies.Delete(StateCookie, new CookieOptions { Path = "/api/auth/discord" });

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state) ||
            string.IsNullOrEmpty(expectedState) || state != expectedState)
        {
            return Redirect($"{frontendUrl}/login?error=discord");
        }

        var discordUser = await discordOAuth.ExchangeCodeAsync(code, ct);
        if (discordUser is null)
            return Redirect($"{frontendUrl}/login?error=discord");

        var result = await sender.Send(new DiscordLoginCommand(discordUser), ct);
        if (!result.IsSuccess)
            return Redirect($"{frontendUrl}/login?error=discord");

        var rawToken = tokenService.GenerateRefreshToken();
        await refreshTokenRepository.AddAsync(new RefreshToken
        {
            UserId = Guid.Parse(result.Value!.UserId),
            Token = rawToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        }, ct);

        Response.Cookies.Append("refreshToken", rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth/refresh",
            MaxAge = TimeSpan.FromDays(7)
        });

        // Токен во fragment (#) — не попадает в серверные логи и Referer
        var fragment = $"accessToken={Uri.EscapeDataString(result.Value.AccessToken)}" +
                       $"&username={Uri.EscapeDataString(result.Value.Username)}" +
                       $"&rank={(int)result.Value.Rank}" +
                       $"&userId={result.Value.UserId}";
        return Redirect($"{frontendUrl}/auth/callback#{fragment}");
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var token = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(token))
            return Unauthorized();

        var command = new RefreshCommand(token);
        var result = await sender.Send(command, ct);

        if (!result.IsSuccess)
            return Problem(detail: result.Error, statusCode: StatusCodes.Status401Unauthorized);

        Response.Cookies.Append("refreshToken", result.Value!.NewRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth/refresh",
            MaxAge = TimeSpan.FromDays(7)
        });

        return Ok(new
        {
            result.Value.AccessToken,
            result.Value.Username,
            result.Value.Rank,
            result.Value.UserId,
        });
    }
}
