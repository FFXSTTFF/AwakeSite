using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.ValueObjects;
using Awake.Infrastructure.ExternalServices.PlayerData;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Awake.Unit.Tests.Features.PlayerData;

public class PlayerDataAggregatorTests
{
    private static readonly PlayerProfile FakeProfile =
        new(100, 50, 2.0, "86%", "10 days", []);

    private static readonly PlayerProfile Profile =
        new(100, 50, 2.0, "45%", "10 days", []);

    private readonly Mock<IPlayerStatsSnapshotRepository> _snapshots = new();
    private readonly Mock<IPlayerDataSource> _source = new();

    private static PlayerDataAggregator BuildAggregator(params IPlayerDataSource[] sources)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new Mock<IPlayerStatsSnapshotRepository>().Object);
        var provider = services.BuildServiceProvider();
        return new PlayerDataAggregator(sources, provider.GetRequiredService<IServiceScopeFactory>());
    }

    private PlayerDataAggregator BuildAggregatorWithMockedSnapshots()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _snapshots.Object);
        var provider = services.BuildServiceProvider();
        return new PlayerDataAggregator(
            [_source.Object],
            provider.GetRequiredService<IServiceScopeFactory>());
    }

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

    [Fact]
    public async Task GetPlayerData_SuccessfulFetch_SavesSnapshot()
    {
        _source.Setup(s => s.TryGetDataAsync("Nick", It.IsAny<CancellationToken>()))
               .ReturnsAsync(Profile);

        var result = await BuildAggregatorWithMockedSnapshots().GetPlayerDataAsync("Nick");

        result.Profile.Should().Be(Profile);
        _snapshots.Verify(r => r.UpsertAsync("Nick", Profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPlayerData_FailedFetch_DoesNotSaveSnapshot()
    {
        _source.Setup(s => s.TryGetDataAsync("Nick", It.IsAny<CancellationToken>()))
               .ReturnsAsync((PlayerProfile?)null);

        var result = await BuildAggregatorWithMockedSnapshots().GetPlayerDataAsync("Nick");

        result.Profile.Should().BeNull();
        _snapshots.Verify(r => r.UpsertAsync(It.IsAny<string>(), It.IsAny<PlayerProfile>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForceRefresh_FirstCall_FetchesAndReturnsTrue()
    {
        _source.Setup(s => s.TryGetDataAsync("Nick", It.IsAny<CancellationToken>()))
               .ReturnsAsync(Profile);

        var ok = await BuildAggregatorWithMockedSnapshots().ForceRefreshAsync("Nick");

        ok.Should().BeTrue();
        _source.Verify(s => s.TryGetDataAsync("Nick", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForceRefresh_SecondCallWithin10Minutes_ReturnsFalse()
    {
        _source.Setup(s => s.TryGetDataAsync("Nick", It.IsAny<CancellationToken>()))
               .ReturnsAsync(Profile);
        var aggregator = BuildAggregatorWithMockedSnapshots();

        await aggregator.ForceRefreshAsync("Nick");
        var second = await aggregator.ForceRefreshAsync("Nick");

        second.Should().BeFalse();
        _source.Verify(s => s.TryGetDataAsync("Nick", It.IsAny<CancellationToken>()), Times.Once);
    }
}
