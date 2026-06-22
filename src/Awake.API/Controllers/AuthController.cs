using Awake.Application.Common.Interfaces;
using Awake.Application.Features.Auth.Commands.Login;
using Awake.Application.Features.Auth.Commands.Register;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Awake.API.Controllers;

public record RegisterRequest(string Username, string Password, string? Email);
public record LoginRequest(string Username, string Password);

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(ISender sender, ITokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var command = new RegisterCommand(request.Username, request.Password, request.Email);
        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Register), result.Value)
            : BadRequest(result.Error);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var command = new LoginCommand(request.Username, request.Password);
        var result = await sender.Send(command, ct);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var refreshToken = tokenService.GenerateRefreshToken();

        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth/refresh",
            MaxAge = TimeSpan.FromDays(7)
        });

        return Ok(result.Value);
    }

    [HttpPost("refresh")]
    public IActionResult Refresh()
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }
}
