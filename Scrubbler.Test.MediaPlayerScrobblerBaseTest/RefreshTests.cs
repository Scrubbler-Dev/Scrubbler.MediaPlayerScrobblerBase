using Moq;
using Scrubbler.MediaPlayerScrobblerBase;
using Scrubbler.PluginBase.Discord;
using Scrubbler.PluginBase.Plugin.Account;
using Scrubbler.PluginBase.Services;
using Shoegaze.LastFM;

namespace Scrubbler.Test.MediaPlayerScrobblerBaseTest;

public partial class MediaPlayerScrobblePluginViewModelBaseTests
{
  private static TestableMediaPlayerScrobblePluginViewModel CreateTrackViewModel() => new(
    Mock.Of<ILastfmClient>(), Mock.Of<IDiscordRichPresence>(),
    new DiscordRichPresenceData("large", "large text", "small", "small text"), Mock.Of<ILogService>())
    { ArtistName = "Artist", TrackName = "Track" };

  [TestCase(false)]
  [TestCase(true)]
  public async Task PlayCounts_DiscardPendingResultWhenTrackOrAccountChanges(bool switchAccount)
  {
    var account = new Mock<IAccountPlugin>();
    var counts = account.As<ICanFetchPlayCounts>();
    var pending = new TaskCompletionSource<(string?, int)>();
    counts.Setup(p => p.GetArtistPlayCount("Artist")).Returns(pending.Task);
    var vm = CreateTrackViewModel();
    vm.FunctionContainer = new(account.Object);
    var refresh = InvokePrivateTask(vm, "UpdatePlayCounts");
    if (switchAccount) vm.FunctionContainer = null;
    else vm.TrackName = "Next";
    vm.CurrentArtistPlayCount = 42;
    pending.SetResult((null, 1));
    await refresh;
    Assert.That(vm.CurrentArtistPlayCount, Is.EqualTo(42));
    counts.Verify(p => p.GetTrackPlayCount(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
  }

  [Test]
  public async Task Tags_DiscardOldTrackResults()
  {
    var account = new Mock<IAccountPlugin>();
    var tags = account.As<ICanFetchTags>();
    var pending = new TaskCompletionSource<(string?, IEnumerable<string>)>();
    tags.Setup(p => p.GetTrackTags("Artist", "Track")).Returns(pending.Task);
    tags.Setup(p => p.GetTrackTags("Artist", "Next")).ReturnsAsync((null, new[] { "new" }));
    var vm = CreateTrackViewModel();
    vm.FunctionContainer = new(account.Object);
    var old = InvokePrivateTask(vm, "UpdateTags");
    vm.TrackName = "Next";
    await InvokePrivateTask(vm, "UpdateTags");
    pending.SetResult((null, new[] { "old" }));
    await old;
    Assert.That(vm.CurrentTrackTags.Select(t => t.Name), Is.EqualTo(new[] { "new" }));
  }

  [Test]
  public async Task LoveToggle_DoesNotChangeNextTracksState()
  {
    var account = new Mock<IAccountPlugin>();
    var love = account.As<ICanLoveTracks>();
    var pending = new TaskCompletionSource<string?>();
    love.Setup(p => p.SetLoveState("Artist", "Track", null, true)).Returns(pending.Task);
    var vm = CreateTrackViewModel();
    vm.FunctionContainer = new(account.Object);
    var toggle = vm.ToggleLovedStateCommand.ExecuteAsync(null);
    vm.TrackName = "Next";
    pending.SetResult(null);
    await toggle;
    Assert.That(vm.CurrentTrackLoved, Is.False);
  }

  [Test]
  public async Task LoveLookup_DoesNotOverwriteCompletedToggle()
  {
    var account = new Mock<IAccountPlugin>();
    var love = account.As<ICanLoveTracks>();
    var pending = new TaskCompletionSource<(string?, bool)>();
    love.Setup(p => p.GetLoveState("Artist", "Track", null)).Returns(pending.Task);
    love.Setup(p => p.SetLoveState("Artist", "Track", null, true)).ReturnsAsync((string?)null);
    var vm = CreateTrackViewModel();
    vm.FunctionContainer = new(account.Object);
    var lookup = InvokePrivateTask(vm, "UpdateLovedInfo");
    await vm.ToggleLovedStateCommand.ExecuteAsync(null);
    pending.SetResult((null, false));
    await lookup;
    Assert.That(vm.CurrentTrackLoved, Is.True);
  }
}

