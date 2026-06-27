using Awake.Domain.ValueObjects;
using Awake.Infrastructure.ExternalServices.PlayerData;
using FluentAssertions;
using Moq;

namespace Awake.Unit.Tests.Features.PlayerData;

public class PlayerDataAggregatorTests
{
    private static readonly PlayerProfile FakeProfile =
        new(100, 50, 2.0, "86%", "10 days", []);

    private static PlayerDataAggregator BuildAggregator(params IPlayerDataSource[] sources) =>
        new(sources);

    [Fact]
    public async Task GetPlayerDataAsync_CacheMiss_FetchesFromSource()
    {
        var source = new Mock<IPlayerDataSource>();
        source.Setup(s => s.TryGetDataAsync("Alice", It.IsAny<CancellationToken>()))
              .ReturnsAsync(FakeProfile);
        var agg = BuildAggregator(source.Object);

        var result = await agg.GetPlayerDataAsync("Alice");

        result.Profile.Should().Be(FakeProfile);
        source.Verify(s => s.TryGetDataAsync("Alice", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPlayerDataAsync_FreshCacheHit_DoesNotFetchAgain()
    {
        var source = new Mock<IPlayerDataSource>();
        source.Setup(s => s.TryGetDataAsync("Alice", It.IsAny<CancellationToken>()))
              .ReturnsAsync(FakeProfile);
        var agg = BuildAggregator(source.Object);

        await agg.GetPlayerDataAsync("Alice");
        var result = await agg.GetPlayerDataAsync("Alice");

        result.Profile.Should().Be(FakeProfile);
        source.Verify(s => s.TryGetDataAsync("Alice", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPlayerDataAsync_SourceReturnsNull_ReturnsNullProfile()
    {
        var source = new Mock<IPlayerDataSource>();
        source.Setup(s => s.TryGetDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((PlayerProfile?)null);
        var agg = BuildAggregator(source.Object);

        var result = await agg.GetPlayerDataAsync("Ghost");

        result.Profile.Should().BeNull();
    }

    [Fact]
    public async Task GetPlayerDataAsync_FirstSourceNull_TriesNextSource()
    {
        var nullSource = new Mock<IPlayerDataSource>();
        nullSource.Setup(s => s.TryGetDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((PlayerProfile?)null);

        var realSource = new Mock<IPlayerDataSource>();
        realSource.Setup(s => s.TryGetDataAsync("Alice", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(FakeProfile);

        var agg = BuildAggregator(nullSource.Object, realSource.Object);

        var result = await agg.GetPlayerDataAsync("Alice");

        result.Profile.Should().Be(FakeProfile);
    }
}
