using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaPlayerScrobblerBase;
using Scrubbler.Abstractions;
using Scrubbler.MediaPlayerScrobblerBase;
using Scrubbler.PluginBase;
using Scrubbler.PluginBase.Discord;
using Scrubbler.PluginBase.Plugin;
using Scrubbler.PluginBase.Plugin.Account;
using Scrubbler.PluginBase.Services;
using Shoegaze.LastFM;

namespace Scrubbler.Plugins.Scrobblers.MediaPlayerScrobbleBase;

public abstract partial class MediaPlayerScrobblePluginViewModelBase(ILastfmClient lastfmClient, IDiscordRichPresence discordRichPresence, DiscordRichPresenceData rpData, ILogService logger) : PluginViewModelBase, IAutoScrobblePluginViewModel
{
  #region Properties

  public event EventHandler<IEnumerable<ScrobbleData>>? ScrobblesDetected;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
  [NotifyPropertyChangedFor(nameof(ConnectionButtonText))]
  [NotifyPropertyChangedFor(nameof(NotConnectedVisibility))]
  protected bool _isConnected;

  [ObservableProperty]
  private bool _autoConnect;

  [ObservableProperty]
  private bool _enableDiscordRichPresence;

  protected readonly ILogService _logger = logger;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(CanLoveTracks))]
  [NotifyPropertyChangedFor(nameof(CanFetchPlayCounts))]
  [NotifyPropertyChangedFor(nameof(CanFetchTags))]
  [NotifyPropertyChangedFor(nameof(CanOpenLinks))]
  [NotifyPropertyChangedFor(nameof(TrackPlayCountVisibility))]
  [NotifyPropertyChangedFor(nameof(ArtistPlayCountVisibility))]
  [NotifyPropertyChangedFor(nameof(AlbumPlayCountVisibility))]
  [NotifyPropertyChangedFor(nameof(TagsVisibility))]
  [NotifyPropertyChangedFor(nameof(LoveButtonVisibility))]
  private AccountFunctionContainer? _functionContainer;

  public bool CanLoveTracks => FunctionContainer?.LoveTrackObject != null;

  public bool CanFetchPlayCounts => FunctionContainer?.FetchPlayCountsObject != null;

  public bool CanFetchTags => FunctionContainer?.FetchTagsObject != null;

  public bool CanOpenLinks => FunctionContainer?.OpenLinksObject != null;

  public string ConnectionStatusText => IsConnected ? "Connected" : "Not connected";
  public string ConnectionButtonText => IsConnected ? "Disconnect" : "Connect";
  public string LoveButtonText => CurrentTrackLoved ? "Unlove" : "Love";
  public Visibility NotConnectedVisibility => IsConnected ? Visibility.Collapsed : Visibility.Visible;
  public Visibility TrackPlayCountVisibility => CanFetchPlayCounts && CurrentTrackPlayCount >= 0 ? Visibility.Visible : Visibility.Collapsed;
  public Visibility ArtistPlayCountVisibility => CanFetchPlayCounts && CurrentArtistPlayCount >= 0 ? Visibility.Visible : Visibility.Collapsed;
  public Visibility AlbumPlayCountVisibility => CanFetchPlayCounts && CurrentAlbumPlayCount >= 0 ? Visibility.Visible : Visibility.Collapsed;
  public Visibility TagsVisibility => CanFetchTags ? Visibility.Visible : Visibility.Collapsed;
  public Visibility LoveButtonVisibility => CanLoveTracks ? Visibility.Visible : Visibility.Collapsed;
  public int ScrobbleProgressSeconds => CurrentTrackScrobbled ? CurrentTrackLengthToScrobble : CountedSeconds;

  protected readonly ILastfmClient _lastfmClient = lastfmClient;

  protected readonly IDiscordRichPresence _discordRichPresence = discordRichPresence;
  private readonly DiscordRichPresenceData _discordRichPresenceData = rpData;

  #region Track Properties

  /// <summary>
  /// The name of the current playing track.
  /// </summary>
  public abstract string CurrentTrackName { get; }

  /// <summary>
  /// The name of the current artist.
  /// </summary>
  public abstract string CurrentArtistName { get; }

  /// <summary>
  /// The name of the current album.
  /// </summary>
  public abstract string CurrentAlbumName { get; }

  /// <summary>
  /// The length of the current track.
  /// </summary>
  public abstract int CurrentTrackLength { get; }

  /// <summary>
  /// Seconds needed to listen to the current song to scrobble it.
  /// (Max <see cref="MAXSECONDSTOSCROBBLE"/>)
  /// </summary>
  public int CurrentTrackLengthToScrobble
  {
    get
    {
      int sec = (int)Math.Ceiling(CurrentTrackLength * PercentageToScrobble);
      return sec < MAXSECONDSTOSCROBBLE ? sec : MAXSECONDSTOSCROBBLE;
    }
  }

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(ScrobbleProgressSeconds))]
  protected bool _currentTrackScrobbled;

  public ObservableCollection<TagViewModel> CurrentTrackTags { get; } = [];

  [ObservableProperty]
  protected Uri? _currentAlbumArtwork;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(TrackPlayCountVisibility))]
  protected int _currentTrackPlayCount = -1;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(ArtistPlayCountVisibility))]
  protected int _currentArtistPlayCount = -1;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(AlbumPlayCountVisibility))]
  protected int _currentAlbumPlayCount = -1;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(LoveButtonText))]
  private bool _currentTrackLoved;

  #endregion Track Properties

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(ScrobbleProgressSeconds))]
  protected int _countedSeconds;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(ScrobbleProgressSeconds))]
  [NotifyPropertyChangedFor(nameof(CurrentTrackLengthToScrobble))]
  private double _percentageToScrobble = 0.5d;

  /// <summary>
  /// Maximum seconds it should take to scrobble a track.
  /// </summary>
  private const int MAXSECONDSTOSCROBBLE = 240;

  #endregion Properties

  [RelayCommand]
  private async Task ToggleConnection()
  {
    if (IsConnected)
      await Disconnect();
    else
      await Connect();
  }

  public void SetInitialAutoConnectState(bool autoConnect)
  {
    AutoConnect = autoConnect;

    if (AutoConnect)
    {
      _logger.Info("Auto-connect is enabled. Attempting to connect...");
      _ = RunSafely(Connect, "auto-connecting");
    }
  }

  public void SetInitialDiscordRichPresenceState(bool discordRichPresence)
  {
    EnableDiscordRichPresence = discordRichPresence;
  }

  /// <summary>
  /// Connects to the client.
  /// </summary>
  protected abstract Task Connect();

  /// <summary>
  /// Disconnects from the client.
  /// </summary>
  protected abstract Task Disconnect();

  /// <summary>
  /// Notifies the ui of changed song info.
  /// </summary>
  protected virtual void UpdateCurrentTrackInfo()
  {
    _refreshVersion++;
    OnPropertyChanged(nameof(CurrentTrackName));
    OnPropertyChanged(nameof(CurrentArtistName));
    OnPropertyChanged(nameof(CurrentAlbumName));
    OnPropertyChanged(nameof(CurrentTrackLength));
    OnPropertyChanged(nameof(CurrentTrackLengthToScrobble));
    OnPropertyChanged(nameof(ScrobbleProgressSeconds));
    _ = UpdateNowPlaying();
    _ = UpdatePlayCounts();
    _ = UpdateTags();
    _ = UpdateLovedInfo();
    _ = FetchAlbumArtwork();
    _ = UpdateDiscordRichPresence();
  }

  private long _refreshVersion;
  private long _accountVersion;
  private long _loveVersion;

  private sealed record TrackRequest(long Version, long AccountVersion, AccountFunctionContainer? Functions,
    string Artist, string Track, string Album);

  private TrackRequest CaptureTrack() => new(_refreshVersion, _accountVersion, FunctionContainer,
    CurrentArtistName, CurrentTrackName, CurrentAlbumName);

  private bool IsCurrent(TrackRequest request) => IsCurrentTrack(request)
    && request.AccountVersion == _accountVersion
    && ReferenceEquals(request.Functions, FunctionContainer);

  private bool IsCurrentTrack(TrackRequest request) => request.Version == _refreshVersion
    && request.Artist == CurrentArtistName && request.Track == CurrentTrackName
    && request.Album == CurrentAlbumName;

  partial void OnFunctionContainerChanged(AccountFunctionContainer? value)
  {
    _accountVersion++;
    ResetAccountMetadata();
    _ = UpdatePlayCounts();
    _ = UpdateTags();
    _ = UpdateLovedInfo();
  }

  private void ResetAccountMetadata()
  {
    CurrentTrackPlayCount = -1;
    CurrentArtistPlayCount = -1;
    CurrentAlbumPlayCount = -1;
    CurrentTrackLoved = false;
    foreach (var vm in CurrentTrackTags)
      vm.OpenLinkRequested -= Tag_OpenLinkRequested;
    CurrentTrackTags.Clear();
  }

  protected void ClearState()
  {
    ResetAccountMetadata();
    CurrentAlbumArtwork = null;
    CountedSeconds = 0;
    CurrentTrackScrobbled = false;
    UpdateCurrentTrackInfo();
  }

  protected async Task UpdateNowPlaying()
  {
    var nowPlaying = FunctionContainer?.UpdateNowPlayingObject;
    if (nowPlaying == null || string.IsNullOrEmpty(CurrentTrackName) || string.IsNullOrEmpty(CurrentArtistName))
      return;

    try
    {
      var albumName = string.IsNullOrWhiteSpace(CurrentAlbumName) ? null : CurrentAlbumName;
      var errorMessage = await nowPlaying.UpdateNowPlaying(CurrentArtistName, CurrentTrackName, albumName);
      if (!string.IsNullOrEmpty(errorMessage))
      {
        _logger.Error($"Error updating Now Playing: {errorMessage}");
        return;
      }
    }
    catch (Exception ex)
    {
      _logger.Error("Error updating Now Playing.", ex);
    }
  }

  private async Task UpdatePlayCounts()
  {
    var request = CaptureTrack();
    if (!CanFetchPlayCounts || string.IsNullOrEmpty(request.Track) || string.IsNullOrEmpty(request.Artist))
      return;

    try
    {
      _logger.Debug("Updating play counts...");
      var (artistError, artistPlayCount) = await request.Functions!.FetchPlayCountsObject!.GetArtistPlayCount(request.Artist);
      if (!IsCurrent(request)) return;
      if (!string.IsNullOrEmpty(artistError))
      {
        _logger.Error($"Error fetching artist play count: {artistError}");
      }
      else
      {
        CurrentArtistPlayCount = artistPlayCount;
        _logger.Debug($"Updated artist play count: {CurrentArtistPlayCount}");
      }
      var (trackError, trackPlayCount) = await request.Functions!.FetchPlayCountsObject.GetTrackPlayCount(request.Artist, request.Track);
      if (!IsCurrent(request)) return;
      if (!string.IsNullOrEmpty(trackError))
      {
        _logger.Error($"Error fetching track play count: {trackError}");
      }
      else
      {
        CurrentTrackPlayCount = trackPlayCount;
        _logger.Debug($"Updated track play count: {CurrentTrackPlayCount}");
      }

      if (!string.IsNullOrEmpty(request.Album))
      {
        var (albumError, albumPlayCount) = await request.Functions!.FetchPlayCountsObject.GetAlbumPlayCount(request.Artist, request.Album);
        if (!IsCurrent(request)) return;
        if (!string.IsNullOrEmpty(albumError))
        {
          _logger.Error($"Error fetching album play count: {albumError}");
        }
        else
        {
          CurrentAlbumPlayCount = albumPlayCount;
          _logger.Debug($"Updated album play count: {CurrentAlbumPlayCount}");
        }
      }
    }
    catch (Exception ex)
    {
      _logger.Error("Error updating play counts.", ex);
    }
  }


  private async Task UpdateTags()
  {
    var request = CaptureTrack();
    if (!CanFetchTags || string.IsNullOrEmpty(request.Track) || string.IsNullOrEmpty(request.Artist))
    {
      _logger.Info("Cannot update tags: Missing account function or track/artist name is empty.");
      return;
    }
    try
    {
      _logger.Debug("Updating tags...");
      var (errorMessage, tags) = await request.Functions!.FetchTagsObject!.GetTrackTags(request.Artist, request.Track);
      if (!IsCurrent(request)) return;
      if (!string.IsNullOrEmpty(errorMessage))
      {
        _logger.Error($"Error fetching tags: {errorMessage}");
        return;
      }

      foreach (var oldTag in CurrentTrackTags)
        oldTag.OpenLinkRequested -= Tag_OpenLinkRequested;
      CurrentTrackTags.Clear();

      // use only the first 5 tags
      foreach (var tag in tags.Take(5))
      {
        var vm = new TagViewModel(tag);
        vm.OpenLinkRequested += Tag_OpenLinkRequested;
        CurrentTrackTags.Add(vm);
      }
      _logger.Debug("Updated tags successfully.");
    }
    catch (Exception ex)
    {
      _logger.Error("Error updating tags.", ex);
    }
  }


  private async void Tag_OpenLinkRequested(object? sender, string e)
  {
    if (!CanOpenLinks)
    {
      _logger.Info("Cannot open tag link: Missing account function.");
      return;
    }

    try
    {
      _logger.Debug($"Opening tag link for {e}...");
      await FunctionContainer!.OpenLinksObject!.OpenTagLink(e);
      _logger.Debug("Opened tag link successfully.");
    }
    catch (Exception ex)
    {
      _logger.Error("Error opening tag link.", ex);
    }
  }
  private async Task UpdateLovedInfo()
  {
    var request = CaptureTrack();
    var loveVersion = _loveVersion;
    if (!CanLoveTracks || string.IsNullOrEmpty(request.Track) || string.IsNullOrEmpty(request.Artist))
      return;

    try
    {
      _logger.Debug("Updating loved info...");
      var albumName = string.IsNullOrWhiteSpace(request.Album) ? null : request.Album;
      var (errorMessage, isLoved) = await request.Functions!.LoveTrackObject!.GetLoveState(request.Artist, request.Track, albumName);
      if (!IsCurrent(request) || loveVersion != _loveVersion) return;
      if (!string.IsNullOrEmpty(errorMessage))
      {
        _logger.Error($"Error fetching loved info: {errorMessage}");
        return;
      }

      CurrentTrackLoved = isLoved;
      _logger.Debug($"Updated loved info: {CurrentTrackLoved}");
    }
    catch (Exception ex)
    {
      _logger.Error("Error updating loved info.", ex);
    }
  }


  protected Task UpdateDiscordRichPresence() => RunSafely(() =>
  {
    if (EnableDiscordRichPresence)
    {
      if (string.IsNullOrEmpty(CurrentTrackName) || string.IsNullOrEmpty(CurrentArtistName))
        _discordRichPresence.Clear();
      else
      {
        var p = new NowPlayingPresence($"Listening to '{CurrentTrackName}'")
        {
          State = $"By '{CurrentArtistName}' on {(string.IsNullOrEmpty(CurrentAlbumName) ? "'Unknown Album'" : $"'{CurrentAlbumName}'")}",
          LargeImageKey = _discordRichPresenceData.LargeImageKey,
          LargeImageText = _discordRichPresenceData.LargeImageText,
          SmallImageKey = _discordRichPresenceData.SmallImageKey,
          SmallImageText = _discordRichPresenceData.SmallImageText,
          StartTimestamp = DateTime.UtcNow,
          EndTimestamp = DateTime.UtcNow.AddSeconds(CurrentTrackLength)
        };

        _discordRichPresence.Publish(p);
      }
    }
    return Task.CompletedTask;
  }, "updating Discord rich presence");

  [RelayCommand]
  private async Task ToggleLovedState()
  {
    var request = CaptureTrack();
    if (!CanLoveTracks || string.IsNullOrEmpty(request.Track) || string.IsNullOrEmpty(request.Artist))
    {
      _logger.Info("Cannot toggle loved state: Missing account function or track/artist name is empty.");
      return;
    }

    var loved = !CurrentTrackLoved;
    _loveVersion++;
    try
    {
      _logger.Info($"Setting loved state to {loved}...");
      var albumName = string.IsNullOrWhiteSpace(request.Album) ? null : request.Album;
      var errorMessage = await request.Functions!.LoveTrackObject!.SetLoveState(request.Artist, request.Track, albumName, loved);
      if (!IsCurrent(request)) return;
      if (!string.IsNullOrEmpty(errorMessage))
      {
        _logger.Error($"Error setting loved state: {errorMessage}");
        return;
      }

      CurrentTrackLoved = loved;
      _logger.Info($"Set loved state successfully: {CurrentTrackLoved}");
    }
    catch (Exception ex)
    {
      _logger.Error("Error setting loved state.", ex);
    }
  }


  [RelayCommand]
  private async Task ArtistClicked()
  {
    if (!CanOpenLinks || string.IsNullOrEmpty(CurrentArtistName))
    {
      _logger.Info("Cannot open artist link: Missing account function or artist name is empty.");
      return;
    }

    try
    {
      _logger.Debug($"Opening artist link for {CurrentArtistName}...");
      await FunctionContainer!.OpenLinksObject!.OpenArtistLink(CurrentArtistName);
      _logger.Debug("Opened artist link successfully.");
    }
    catch (Exception ex)
    {
      _logger.Error("Error opening artist link.", ex);
    }
  }

  [RelayCommand]
  private async Task AlbumClicked()
  {
    if (!CanOpenLinks || string.IsNullOrEmpty(CurrentArtistName) || string.IsNullOrEmpty(CurrentAlbumName))
    {
      _logger.Info("Cannot open album link: Missing account function or artist/album name is empty.");
      return;
    }

    try
    {
      _logger.Debug($"Opening album link for {CurrentAlbumName} by {CurrentArtistName}...");
      await FunctionContainer!.OpenLinksObject!.OpenAlbumLink(CurrentAlbumName, CurrentArtistName);
      _logger.Debug("Opened album link successfully.");
    }
    catch (Exception ex)
    {
      _logger.Error("Error opening album link.", ex);
    }
  }

  [RelayCommand]
  private async Task TrackClicked()
  {
    if (!CanOpenLinks || string.IsNullOrEmpty(CurrentArtistName) || string.IsNullOrEmpty(CurrentTrackName))
    {
      _logger.Info("Cannot open track link: Missing account function or artist/track name is empty.");
      return;
    }

    try
    {
      _logger.Debug($"Opening track link for {CurrentTrackName} by {CurrentArtistName}...");
      await FunctionContainer!.OpenLinksObject!.OpenTrackLink(CurrentTrackName, CurrentArtistName, CurrentAlbumName);
      _logger.Debug("Opened track link successfully.");
    }
    catch (Exception ex)
    {
      _logger.Error("Error opening track link.", ex);
    }
  }

  private Task FetchAlbumArtwork()
  {
    var request = CaptureTrack();
    return RunSafely(async () =>
    {
      if (string.IsNullOrEmpty(request.Artist) || string.IsNullOrEmpty(request.Track))
      {
        _logger.Debug("Cannot fetch album artwork: Track name or artist name is empty.");
        CurrentAlbumArtwork = null;
        return;
      }

      if (!string.IsNullOrEmpty(request.Album))
      {
        var albumResponse = await _lastfmClient.Album.GetInfoByNameAsync(request.Album, request.Artist);
        if (!IsCurrentTrack(request)) return;
        if (albumResponse.IsSuccess && albumResponse.Data != null)
        {
          var albumArtwork = GetBestImage(albumResponse.Data.Images);
          if (albumArtwork != null)
          {
            CurrentAlbumArtwork = albumArtwork;
            _logger.Debug("Fetched album artwork successfully.");
            return;
          }
        }

        _logger.Debug($"Failed to fetch album artwork: {albumResponse.ErrorMessage}");
      }

      var trackResponse = await _lastfmClient.Track.GetInfoByNameAsync(request.Track, request.Artist);
      if (!IsCurrentTrack(request)) return;
      if (trackResponse.IsSuccess && trackResponse.Data != null)
      {
        CurrentAlbumArtwork = GetBestImage(trackResponse.Data.Images);
        if (CurrentAlbumArtwork != null)
          _logger.Debug("Fetched track artwork successfully.");
        else
          _logger.Debug("Track info did not contain artwork.");
      }
      else
      {
        CurrentAlbumArtwork = null;
        _logger.Debug($"Failed to fetch track artwork: {trackResponse.ErrorMessage}");
      }
    }, "fetching album artwork", () =>
    {
      if (IsCurrentTrack(request))
        CurrentAlbumArtwork = null;
    });
  }

  private async Task RunSafely(Func<Task> operation, string description, Action? onFailure = null)
  {
    try
    {
      await operation();
    }
    catch (OperationCanceledException)
    {
      onFailure?.Invoke();
      _logger.Debug($"Canceled {description}.");
    }
    catch (Exception ex)
    {
      onFailure?.Invoke();
      _logger.Error($"Error {description}.", ex);
    }
  }

  private static Uri? GetBestImage(IReadOnlyDictionary<ImageSize, Uri>? images)
  {
    if (images == null || images.Count == 0)
      return null;

    ImageSize[] preferredSizes = [ImageSize.Mega, ImageSize.ExtraLarge, ImageSize.Large, ImageSize.Medium, ImageSize.Small, ImageSize.Unknown];
    foreach (var size in preferredSizes)
    {
      if (images.TryGetValue(size, out var image) && image != null)
        return image;
    }

    return images.Values.LastOrDefault(image => image != null);
  }

  protected void OnScrobblesDetected(IEnumerable<ScrobbleData> scrobbles)
  {
    _logger.Info($"Detected {scrobbles.Count()} scrobble(s).");
    ScrobblesDetected?.Invoke(this, scrobbles);
  }

  partial void OnEnableDiscordRichPresenceChanged(bool value)
  {
    _logger.Debug($"EnableDiscordRichPresence changed to {value}.");

    if (!value)
      _ = RunSafely(() =>
      {
        _discordRichPresence.Clear();
        return Task.CompletedTask;
      }, "updating Discord rich presence");
  }
}
