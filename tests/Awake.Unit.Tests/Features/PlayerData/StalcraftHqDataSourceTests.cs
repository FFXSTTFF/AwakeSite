using Awake.Infrastructure.ExternalServices.PlayerData.Sources;
using FluentAssertions;

namespace Awake.Unit.Tests.Features.PlayerData;

public class StalcraftHqDataSourceTests
{
    private const string ValidHtml = """
        <html><body>
          <dl>
            <div><dt>Kills:</dt><dd>121 559</dd></div>
            <div><dt>Deaths:</dt><dd>48 879</dd></div>
            <div><dt>Accuracy:</dt><dd>86%</dd></div>
          </dl>
          <p>In-game for 388 days, 5 hours and 45 minutes</p>
          <div><span>[HARD] Try Hard</span></div>
          <div><span>[LOVE] Awake</span></div>
        </body></html>
        """;

    private const string EmptyHtml = "<html><body><p>Player not found</p></body></html>";

    [Fact]
    public void Parse_ValidHtml_ReturnsCorrectKills()
    {
        var profile = StalcraftHqDataSource.Parse(ValidHtml);
        profile!.Kills.Should().Be(121559);
    }

    [Fact]
    public void Parse_ValidHtml_ReturnsCorrectDeaths()
    {
        var profile = StalcraftHqDataSource.Parse(ValidHtml);
        profile!.Deaths.Should().Be(48879);
    }

    [Fact]
    public void Parse_ValidHtml_ComputesKdRatio()
    {
        var profile = StalcraftHqDataSource.Parse(ValidHtml);
        profile!.KdRatio.Should().Be(Math.Round(121559.0 / 48879.0, 2));
    }

    [Fact]
    public void Parse_ValidHtml_ReturnsAccuracy()
    {
        var profile = StalcraftHqDataSource.Parse(ValidHtml);
        profile!.Accuracy.Should().Be("86%");
    }

    [Fact]
    public void Parse_ValidHtml_ReturnsPlaytime()
    {
        var profile = StalcraftHqDataSource.Parse(ValidHtml);
        profile!.Playtime.Should().Be("388 days, 5 hours and 45 minutes");
    }

    [Fact]
    public void Parse_ValidHtml_ReturnsClanHistory()
    {
        var profile = StalcraftHqDataSource.Parse(ValidHtml);
        profile!.ClanHistory.Should().HaveCount(2);
        profile.ClanHistory[0].ClanTag.Should().Be("HARD");
        profile.ClanHistory[0].ClanName.Should().Be("Try Hard");
        profile.ClanHistory[1].ClanTag.Should().Be("LOVE");
    }

    [Fact]
    public void Parse_HtmlWithZeroStats_ReturnsNull()
    {
        StalcraftHqDataSource.Parse(EmptyHtml).Should().BeNull();
    }

    [Fact]
    public void Parse_NumbersWithSpaces_ParsesCorrectly()
    {
        var html = """
            <html><body>
              <dl>
                <div><dt>Kills:</dt><dd>1 000 000</dd></div>
                <div><dt>Deaths:</dt><dd>500 000</dd></div>
              </dl>
            </body></html>
            """;
        var profile = StalcraftHqDataSource.Parse(html);
        profile!.Kills.Should().Be(1000000);
        profile.Deaths.Should().Be(500000);
    }
}
