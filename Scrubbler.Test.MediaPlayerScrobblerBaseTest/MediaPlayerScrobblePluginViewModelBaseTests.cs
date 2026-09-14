using Moq;
using Scrubbler.MediaPlayerScrobblerBase;
using Scrubbler.PluginBase;
using Scrubbler.PluginBase.Discord;
using Scrubbler.PluginBase.Plugin;
using Scrubbler.PluginBase.Plugin.Account;
using Scrubbler.PluginBase.Services;
using Scrubbler.Plugins.Scrobblers.MediaPlayerScrobbleBase;
using Shoegaze.LastFM;

namespace Scrubbler.Test.MediaPlayerScrobblerBaseTest;

/// <summary>
/// Tests for MediaPlayerScrobblePluginViewModelBase.CanFetchPlayCounts property.
/// Covers null and non-null FunctionContainer and its FetchPlayCountsObject variations.
/// </summary>
public partial class MediaPlayerScrobblePluginViewModelBaseTests
{
  /// <summary>
  /// Minimal concrete implementation of the abstract MediaPlayerScrobblePluginViewModelBase to allow instantiation for tests.
  /// All abstract members are implemented with simple, deterministic returns suitable for unit testing the base property behavior.
  /// This helper is defined as an inner class to comply with the constraint that additional types must be inside the test class.
  /// </summary>
  private sealed class TestViewModel(ILastfmClient lastfm, IDiscordRichPresence discord, DiscordRichPresenceData rpData, ILogService logger) : MediaPlayerScrobblePluginViewModelBase(lastfm, discord, rpData, logger)
  {

    // Provide simple concrete implementations of abstract track properties.
    public override string CurrentTrackName => string.Empty;
    public override string CurrentArtistName => string.Empty;
    public override string CurrentAlbumName => string.Empty;
    public override int CurrentTrackLength => 0;

    // Concrete implementations of abstract Connect/Disconnect to satisfy base contract.
    protected override Task Connect() => Task.CompletedTask;
    protected override Task Disconnect() => Task.CompletedTask;
  }

  /// <summary>
  /// Verifies that OnScrobblesDetected logs the correct count and invokes the ScrobblesDetected event
  /// for varying collection sizes (including empty collection).
  /// Input conditions: a collection with 'count' mock ScrobbleData items and an event subscriber attached.
  /// Expected: ILogService.Info is called once with exact message 'Detected {count} scrobble(s).'
  /// and the event is invoked with the same enumerable instance.
  /// </summary>
  [TestCase(0)]
  [TestCase(1)]
  [TestCase(5)]
  public void OnScrobblesDetected_WithSubscriber_LogsCountAndInvokesEvent(int count)
  {
    // Arrange
    var lastfmMock = new Mock<ILastfmClient>(MockBehavior.Strict);
    var discordRpMock = new Mock<IDiscordRichPresence>(MockBehavior.Strict);
    var loggerMock = new Mock<ILogService>(MockBehavior.Strict);
    var rpData = new DiscordRichPresenceData("test_large_image", "test_large_text", "test_small_image", "test_small_text");

    // Create the concrete testable view model
    var vm = new TestableMediaPlayerScrobblePluginViewModel(
        lastfmMock.Object,
        discordRpMock.Object,
        rpData,
        loggerMock.Object);

    IEnumerable<ScrobbleData>? capturedArgument = null;
    vm.ScrobblesDetected += (sender, scrobbles) =>
    {
      capturedArgument = scrobbles;
    };

    var scrobbles = Enumerable.Range(0, count)
                              .Select(_ => new ScrobbleData("test_track", "test_artist", DateTime.Now))
                              .ToList()
                              .AsEnumerable();

    var expectedMessage = $"Detected {count} scrobble(s).";

    loggerMock.Setup(l => l.Info(It.Is<string>(s => s == expectedMessage)));

    // Act
    vm.InvokeOnScrobblesDetected(scrobbles);

    // Assert
    loggerMock.Verify(l => l.Info(It.Is<string>(s => s == expectedMessage)), Times.Once,
        $"Expected Info to be called once with message='{expectedMessage}'.");

    using (Assert.EnterMultipleScope())
    {
      Assert.That(capturedArgument, Is.Not.Null, "Event subscriber should have been invoked and received the scrobbles enumerable.");
      Assert.That(ReferenceEquals(capturedArgument, scrobbles), Is.True, "Event should be invoked with the same enumerable instance provided.");
    }
    Assert.That(capturedArgument!.Count(), Is.EqualTo(count), "Event argument should contain expected number of items.");
  }

  /// <summary>
  /// Verifies that OnScrobblesDetected does not throw when there are no subscribers
  /// and still logs the correct count.
  /// Input conditions: a collection with 2 mock ScrobbleData items and no event subscribers.
  /// Expected: No exception is thrown and ILogService.Info is called once with correct message.
  /// </summary>
  [Test]
  public void OnScrobblesDetected_NoSubscriber_OnlyLogsAndDoesNotThrow()
  {
    // Arrange
    var lastfmMock = new Mock<ILastfmClient>(MockBehavior.Strict);
    var discordRpMock = new Mock<IDiscordRichPresence>(MockBehavior.Strict);
    var loggerMock = new Mock<ILogService>(MockBehavior.Strict);
    var rpData = new DiscordRichPresenceData("test_large_image", "test_large_text", "test_small_image", "test_small_text");

    var vm = new TestableMediaPlayerScrobblePluginViewModel(
            lastfmMock.Object,
            discordRpMock.Object,
            rpData,
            loggerMock.Object);

    var scrobbles = new List<ScrobbleData>
            {
                new("test_track", "test_artist", DateTime.Now),
                new("test_track2", "test_artist2", DateTime.Now),
            }.AsEnumerable();

    var expectedMessage = $"Detected {scrobbles.Count()} scrobble(s).";

    loggerMock.Setup(l => l.Info(It.Is<string>(s => s == expectedMessage)));

    // Act & Assert
    Assert.DoesNotThrow(() => vm.InvokeOnScrobblesDetected(scrobbles), "Method should not throw when there are no subscribers.");

    loggerMock.Verify(l => l.Info(It.Is<string>(s => s == expectedMessage)), Times.Once,
        "Expected Info to be called once even when there are no subscribers.");
  }

  // Helper concrete implementation inside the test class to expose protected members.
  private sealed class TestableMediaPlayerScrobblePluginViewModel(
      ILastfmClient lastfmClient,
      IDiscordRichPresence discordRichPresence,
      DiscordRichPresenceData rpData,
      ILogService logger) : MediaPlayerScrobblePluginViewModelBase(lastfmClient, discordRichPresence, rpData, logger)
  {
    public string TrackName { get; set; } = string.Empty;

    public string ArtistName { get; set; } = string.Empty;

    public string AlbumName { get; set; } = string.Empty;

    // Expose the protected method for testing purposes.
    public void InvokeOnScrobblesDetected(IEnumerable<ScrobbleData> scrobbles)
    {
      OnScrobblesDetected(scrobbles);
    }

    public Task InvokeUpdateNowPlaying()
    {
      return UpdateNowPlaying();
    }

    // Minimal implementations for abstract members:
    public override string CurrentTrackName => TrackName;
    public override string CurrentArtistName => ArtistName;
    public override string CurrentAlbumName => AlbumName;
    public override int CurrentTrackLength => 0;

    protected override Task Connect()
    {
      return Task.CompletedTask;
    }

    protected override Task Disconnect()
    {
      return Task.CompletedTask;
    }
  }
  private const string ExpectedLoggerMessage = "Auto-connect is enabled. Attempting to connect...";

  [Test]
  public async Task UpdateNowPlaying_WithEmptyAlbum_CallsAccountFunctionWithNullAlbum()
  {
    var lastfmMock = new Mock<ILastfmClient>();
    var discordRpMock = new Mock<IDiscordRichPresence>();
    var loggerMock = new Mock<ILogService>();
    var accountMock = new Mock<IAccountPlugin>();
    var updateNowPlayingMock = accountMock.As<ICanUpdateNowPlaying>();
    updateNowPlayingMock
      .Setup(u => u.UpdateNowPlaying("Artist", "Track", null))
      .ReturnsAsync((string?)null);

    var vm = new TestableMediaPlayerScrobblePluginViewModel(
      lastfmMock.Object,
      discordRpMock.Object,
      new DiscordRichPresenceData("large", "large text", "small", "small text"),
      loggerMock.Object)
    {
      ArtistName = "Artist",
      TrackName = "Track",
      AlbumName = string.Empty,
      FunctionContainer = new AccountFunctionContainer(accountMock.Object)
    };

    await vm.InvokeUpdateNowPlaying();

    updateNowPlayingMock.Verify(u => u.UpdateNowPlaying("Artist", "Track", null), Times.Once);

    // Removing or switching the container must not keep the old account alive.
    vm.FunctionContainer = null;
    await vm.InvokeUpdateNowPlaying();
    vm.FunctionContainer = new AccountFunctionContainer(null);
    await vm.InvokeUpdateNowPlaying();
    var replacementAccount = new Mock<IAccountPlugin>();
    var replacementNowPlaying = replacementAccount.As<ICanUpdateNowPlaying>();
    replacementNowPlaying.Setup(u => u.UpdateNowPlaying("Artist", "Track", null)).ReturnsAsync((string?)null);
    vm.FunctionContainer = new AccountFunctionContainer(replacementAccount.Object);
    await vm.InvokeUpdateNowPlaying();

    updateNowPlayingMock.Verify(u => u.UpdateNowPlaying("Artist", "Track", null), Times.Once);
    replacementNowPlaying.Verify(u => u.UpdateNowPlaying("Artist", "Track", null), Times.Once);
  }

  [Test]
  public async Task UpdatePlayCounts_WithEmptyAlbum_FetchesArtistAndTrackCountsOnly()
  {
    var lastfmMock = new Mock<ILastfmClient>();
    var discordRpMock = new Mock<IDiscordRichPresence>();
    var loggerMock = new Mock<ILogService>();
    var accountMock = new Mock<IAccountPlugin>();
    var playCountsMock = accountMock.As<ICanFetchPlayCounts>();
    playCountsMock.Setup(p => p.GetArtistPlayCount("Artist")).ReturnsAsync((null, 2));
    playCountsMock.Setup(p => p.GetTrackPlayCount("Artist", "Track")).ReturnsAsync((null, 3));

    var vm = new TestableMediaPlayerScrobblePluginViewModel(
      lastfmMock.Object,
      discordRpMock.Object,
      new DiscordRichPresenceData("large", "large text", "small", "small text"),
      loggerMock.Object)
    {
      ArtistName = "Artist",
      TrackName = "Track",
      AlbumName = string.Empty,
      FunctionContainer = new AccountFunctionContainer(accountMock.Object)
    };

    await InvokePrivateTask(vm, "UpdatePlayCounts");

    using (Assert.EnterMultipleScope())
    {
      Assert.That(vm.CurrentArtistPlayCount, Is.EqualTo(2));
      Assert.That(vm.CurrentTrackPlayCount, Is.EqualTo(3));
      Assert.That(vm.CurrentAlbumPlayCount, Is.Zero);
    }
    playCountsMock.Verify(p => p.GetAlbumPlayCount(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
  }

  [Test]
  public async Task ToggleLovedState_WithEmptyAlbum_CallsAccountFunctionWithNullAlbum()
  {
    var lastfmMock = new Mock<ILastfmClient>();
    var discordRpMock = new Mock<IDiscordRichPresence>();
    var loggerMock = new Mock<ILogService>();
    var accountMock = new Mock<IAccountPlugin>();
    var loveTracksMock = accountMock.As<ICanLoveTracks>();
    loveTracksMock
      .Setup(l => l.SetLoveState("Artist", "Track", null, true))
      .ReturnsAsync((string?)null);

    var vm = new TestableMediaPlayerScrobblePluginViewModel(
      lastfmMock.Object,
      discordRpMock.Object,
      new DiscordRichPresenceData("large", "large text", "small", "small text"),
      loggerMock.Object)
    {
      ArtistName = "Artist",
      TrackName = "Track",
      AlbumName = string.Empty,
      FunctionContainer = new AccountFunctionContainer(accountMock.Object)
    };

    await vm.ToggleLovedStateCommand.ExecuteAsync(null);

    loveTracksMock.Verify(l => l.SetLoveState("Artist", "Track", null, true), Times.Once);
  }

  [Test]
  public void GetBestImage_PrefersLargestKnownSize()
  {
    var small = new Uri("https://example.test/small.png");
    var mega = new Uri("https://example.test/mega.png");
    var images = new Dictionary<ImageSize, Uri>
    {
      [ImageSize.Small] = small,
      [ImageSize.Mega] = mega
    };

    var result = InvokeGetBestImage(images);

    Assert.That(result, Is.EqualTo(mega));
  }

  private static async Task InvokePrivateTask(MediaPlayerScrobblePluginViewModelBase vm, string methodName)
  {
    var method = typeof(MediaPlayerScrobblePluginViewModelBase).GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
      ?? throw new MissingMethodException(nameof(MediaPlayerScrobblePluginViewModelBase), methodName);

    await (Task)method.Invoke(vm, null)!;
  }

  private static Uri? InvokeGetBestImage(IReadOnlyDictionary<ImageSize, Uri> images)
  {
    var method = typeof(MediaPlayerScrobblePluginViewModelBase).GetMethod("GetBestImage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
      ?? throw new MissingMethodException(nameof(MediaPlayerScrobblePluginViewModelBase), "GetBestImage");

    return (Uri?)method.Invoke(null, [images]);
  }

}
