using System.Collections;
using Moq;
using Scrubbler.Abstractions;
using Scrubbler.Abstractions.Services;
using Scrubbler.MediaPlayerScrobblerBase;
using Scrubbler.PluginBase.Discord;
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

    // Expose the protected method for testing purposes.
    public void InvokeOnScrobblesDetected(IEnumerable<ScrobbleData> scrobbles)
    {
      OnScrobblesDetected(scrobbles);
    }

    // Minimal implementations for abstract members:
    public override string CurrentTrackName => string.Empty;
    public override string CurrentArtistName => string.Empty;
    public override string CurrentAlbumName => string.Empty;
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

}