using System.Text.Json;
using Awake.Infrastructure.ExternalServices.Discord;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.Auth;

public class DiscordOAuthServiceTests
{
    private static JsonElement Json(string s) => JsonSerializer.Deserialize<JsonElement>(s);

    [Fact]
    public void ParseUser_FullPayload_MapsAllFields()
    {
        var json = Json("""
            { "id": "111222333", "username": "oops", "global_name": "OopsITry", "avatar": "abc123" }
            """);

        var user = DiscordOAuthService.ParseUser(json);

        user.Id.Should().Be("111222333");
        user.Username.Should().Be("oops");
        user.GlobalName.Should().Be("OopsITry");
        user.AvatarUrl.Should().Be("https://cdn.discordapp.com/avatars/111222333/abc123.png");
    }

    [Fact]
    public void ParseUser_NullAvatarAndGlobalName_ReturnsNulls()
    {
        var json = Json("""
            { "id": "111", "username": "oops", "global_name": null, "avatar": null }
            """);

        var user = DiscordOAuthService.ParseUser(json);

        user.GlobalName.Should().BeNull();
        user.AvatarUrl.Should().BeNull();
    }
}
