using Moq;
using Scrubbler.MediaPlayerScrobblerBase;
using Scrubbler.PluginBase.Discord;
using Scrubbler.PluginBase.Services;
using Shoegaze.LastFM;

namespace Scrubbler.Test.MediaPlayerScrobblerBaseTest;

public partial class MediaPlayerScrobblePluginViewModelBaseTests
{
  [TestCase(false)]
  [TestCase(true)]
  public async Task AutoConnect_LogsSynchronousAndAsynchronousFailures(bool asynchronous)
  {
    var logger = new Mock<ILogService>();
    var exception = new InvalidOperationException("connection failed");
    var logged = new TaskCompletionSource();
    logger.Setup(l => l.Error("Error auto-connecting.", exception)).Callback(() => logged.SetResult());
    var vm = new TestableMediaPlayerScrobblePluginViewModel(Mock.Of<ILastfmClient>(),
      Mock.Of<IDiscordRichPresence>(), new DiscordRichPresenceData("", "", "", ""), logger.Object);
    var pending = new TaskCompletionSource();
    vm.ConnectOperation = asynchronous ? () => pending.Task : () => throw exception;
    vm.SetInitialAutoConnectState(true);
    if (asynchronous) pending.SetException(exception);
    await logged.Task.WaitAsync(TimeSpan.FromSeconds(5));
    logger.Verify(l => l.Error("Error auto-connecting.", exception), Times.Once);
  }

  [Test]
  public async Task AutoConnect_CancellationIsObservedWithoutAnError()
  {
    var logger = new Mock<ILogService>();
    var observed = new TaskCompletionSource();
    logger.Setup(l => l.Debug("Canceled auto-connecting.")).Callback(() => observed.SetResult());
    var vm = new TestableMediaPlayerScrobblePluginViewModel(Mock.Of<ILastfmClient>(),
      Mock.Of<IDiscordRichPresence>(), new DiscordRichPresenceData("", "", "", ""), logger.Object)
      { ConnectOperation = () => Task.FromCanceled(new CancellationToken(true)) };
    vm.SetInitialAutoConnectState(true);
    await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    logger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
  }

  [Test]
  public async Task ArtworkFailure_IsLoggedAndClearsPreviousImage()
  {
    var lastfm = new Mock<ILastfmClient>();
    var logger = new Mock<ILogService>();
    var exception = new InvalidOperationException("artwork failed");
    lastfm.SetupGet(l => l.Album).Throws(exception);
    var vm = new TestableMediaPlayerScrobblePluginViewModel(lastfm.Object,
      Mock.Of<IDiscordRichPresence>(), new DiscordRichPresenceData("", "", "", ""), logger.Object)
      { ArtistName = "Artist", TrackName = "Track", AlbumName = "Album",
        CurrentAlbumArtwork = new Uri("https://example.test/old.png") };
    await InvokePrivateTask(vm, "FetchAlbumArtwork");
    Assert.That(vm.CurrentAlbumArtwork, Is.Null);
    logger.Verify(l => l.Error("Error fetching album artwork.", exception), Times.Once);
  }

  [TestCase(false)]
  [TestCase(true)]
  public async Task DiscordFailure_IsLoggedForPublishAndClear(bool publish)
  {
    var discord = new Mock<IDiscordRichPresence>();
    var logger = new Mock<ILogService>();
    var exception = new InvalidOperationException("discord failed");
    discord.Setup(d => d.Clear()).Throws(exception);
    discord.Setup(d => d.Publish(It.IsAny<NowPlayingPresence>())).Throws(exception);
    var vm = new TestableMediaPlayerScrobblePluginViewModel(Mock.Of<ILastfmClient>(),
      discord.Object, new DiscordRichPresenceData("", "", "", ""), logger.Object)
      { ArtistName = "Artist", TrackName = publish ? "Track" : "", EnableDiscordRichPresence = true };
    await InvokePrivateTask(vm, "UpdateDiscordRichPresence");
    logger.Verify(l => l.Error("Error updating Discord rich presence.", exception), Times.Once);
    if (publish)
    {
      vm.EnableDiscordRichPresence = false;
      logger.Verify(l => l.Error("Error updating Discord rich presence.", exception), Times.Exactly(2));
    }
  }
}
