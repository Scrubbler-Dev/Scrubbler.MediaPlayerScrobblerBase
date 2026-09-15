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

  [Test]
  public void DisplayState_TracksRepeatedConnectionCapabilityAndTrackChanges()
  {
    var vm = CreateTrackViewModel();
    var notifications = new List<string?>();
    vm.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
    var account = new Mock<IAccountPlugin>();
    account.As<ICanFetchPlayCounts>();
    account.As<ICanFetchTags>();
    account.As<ICanLoveTracks>();
    vm.FunctionContainer = new(account.Object);
    vm.CurrentTrackPlayCount = -1;
    Assert.That(vm.TrackPlayCountVisibility, Is.EqualTo(Microsoft.UI.Xaml.Visibility.Collapsed));
    vm.CurrentTrackPlayCount = 7;
    Assert.That(vm.TrackPlayCountVisibility, Is.EqualTo(Microsoft.UI.Xaml.Visibility.Visible));
    vm.CurrentTrackLoved = true;
    Assert.That(vm.LoveButtonText, Is.EqualTo("Unlove"));
    vm.CurrentTrackLoved = false;
    Assert.That(vm.LoveButtonText, Is.EqualTo("Love"));
    vm.IsConnected = true;
    Assert.That(vm.NotConnectedVisibility, Is.EqualTo(Microsoft.UI.Xaml.Visibility.Collapsed));
    vm.IsConnected = false;
    Assert.That(vm.NotConnectedVisibility, Is.EqualTo(Microsoft.UI.Xaml.Visibility.Visible));
    vm.CountedSeconds = 5;
    vm.CurrentTrackScrobbled = true;
    Assert.That(vm.ScrobbleProgressSeconds, Is.EqualTo(vm.CurrentTrackLengthToScrobble));
    vm.CurrentTrackScrobbled = false;
    Assert.That(vm.ScrobbleProgressSeconds, Is.EqualTo(5));
    vm.FunctionContainer = null;
    Assert.That(vm.TrackPlayCountVisibility, Is.EqualTo(Microsoft.UI.Xaml.Visibility.Collapsed));
    Assert.That(vm.LoveButtonVisibility, Is.EqualTo(Microsoft.UI.Xaml.Visibility.Collapsed));
    Assert.That(vm.TagsVisibility, Is.EqualTo(Microsoft.UI.Xaml.Visibility.Collapsed));
    vm.FunctionContainer = new(account.Object);
    Assert.That(vm.LoveButtonVisibility, Is.EqualTo(Microsoft.UI.Xaml.Visibility.Visible));
    Assert.That(vm.TagsVisibility, Is.EqualTo(Microsoft.UI.Xaml.Visibility.Visible));
    Assert.That(notifications, Does.Contain(nameof(vm.TrackPlayCountVisibility)));
    Assert.That(notifications, Does.Contain(nameof(vm.LoveButtonText)));
    Assert.That(notifications, Does.Contain(nameof(vm.NotConnectedVisibility)));
    Assert.That(notifications, Does.Contain(nameof(vm.ScrobbleProgressSeconds)));
  }

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

