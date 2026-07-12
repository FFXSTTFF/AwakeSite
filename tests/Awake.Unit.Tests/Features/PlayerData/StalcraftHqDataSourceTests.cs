using System.Text.Json;
using Awake.Infrastructure.ExternalServices.PlayerData.Sources;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.PlayerData;

public class StalcraftApiDataSourceTests
{
    private static JsonElement MakeJson(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);

    private const string FullProfileJson = """
        {
          "stats": [
            { "id": "kil",     "value": 121559 },
            { "id": "dea",     "value": 48879  },
            { "id": "sho-fir", "value": 1000000 },
            { "id": "sho-hit", "value": 860000  },
            { "id": "pla-tim", "value": 33543900000 }
          ],
          "clan": {
            "info": { "name": "Awake", "tag": "LOVE" }
          }
        }
        """;

    [Fact]
    public void Parse_ValidJson_ReturnsCorrectKills()
    {
        var profile = StalcraftApiDataSource.ParseProfile(MakeJson(FullProfileJson));
        profile!.Kills.Should().Be(121559);
    }

    [Fact]
    public void Parse_ValidJson_ReturnsCorrectDeaths()
    {
        var profile = StalcraftApiDataSource.ParseProfile(MakeJson(FullProfileJson));
        profile!.Deaths.Should().Be(48879);
    }

    [Fact]
    public void Parse_ValidJson_ComputesKdRatio()
    {
        var profile = StalcraftApiDataSource.ParseProfile(MakeJson(FullProfileJson));
        profile!.KdRatio.Should().Be(Math.Round(121559.0 / 48879.0, 2));
    }

    [Fact]
    public void Parse_ValidJson_ComputesAccuracy()
    {
        var profile = StalcraftApiDataSource.ParseProfile(MakeJson(FullProfileJson));
        profile!.Accuracy.Should().Be("86%");
    }

    [Fact]
    public void Parse_ValidJson_FormatsPlaytime()
    {
        var profile = StalcraftApiDataSource.ParseProfile(MakeJson(FullProfileJson));
        profile!.Playtime.Should().Be("388d 5h");
    }

    [Fact]
    public void Parse_ValidJson_ReturnsClanEntry()
    {
        var profile = StalcraftApiDataSource.ParseProfile(MakeJson(FullProfileJson));
        profile!.ClanHistory.Should().HaveCount(1);
        profile.ClanHistory[0].ClanTag.Should().Be("LOVE");
        profile.ClanHistory[0].ClanName.Should().Be("Awake");
    }

    [Fact]
    public void Parse_ZeroStats_ReturnsNull()
    {
        StalcraftApiDataSource.ParseProfile(MakeJson("""{ "stats": [], "clan": null }"""))
            .Should().BeNull();
    }

    [Fact]
    public void Parse_ZeroShots_ReturnsAccuracyDash()
    {
        var json = """{ "stats": [{ "id": "kil", "value": 100 }, { "id": "dea", "value": 10 }] }""";
        var profile = StalcraftApiDataSource.ParseProfile(MakeJson(json));
        profile!.Accuracy.Should().Be("—");
    }
}
