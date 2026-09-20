using LoupixDeck.PluginSdk;
using Tmds.DBus.Protocol;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// One MPRIS player on the bus. Holds the cached state, keeps it up to date from the player's
/// own signals instead of polling, and sends the control calls.
/// </summary>
internal sealed class PlayerProxy : IDisposable
{
    private readonly DBusClient _client;
    private readonly IPluginLogger _logger;
    private readonly List<IDisposable> _watches = [];
    private readonly Lock _gate = new();

    private PlayerState _state;
    private bool _disposed;

    public PlayerProxy(DBusClient client, string serviceName, IPluginLogger logger)
    {
        _client = client;
        _logger = logger;
        ServiceName = serviceName;
        _state = new PlayerState { ServiceName = serviceName };
    }

    public string ServiceName { get; }

    /// <summary>The current snapshot. Always a complete, consistent state.</summary>
    public PlayerState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>Raised whenever the cached state changed.</summary>
    public event Action<PlayerState>? Changed;

    /// <summary>Subscribes to the player's signals and reads its current properties.</summary>
    public async Task InitializeAsync()
    {
        await SubscribeAsync().ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    /// <summary>Re-reads every property. Used on startup and after a reconnect.</summary>
    public async Task RefreshAsync()
    {
        Dictionary<string, VariantValue> root = await _client
            .GetAllPropertiesAsync(ServiceName, MprisServices.ObjectPath, MprisServices.RootInterface)
            .ConfigureAwait(false);

        Dictionary<string, VariantValue> player = await _client
            .GetAllPropertiesAsync(ServiceName, MprisServices.ObjectPath, MprisServices.PlayerInterface)
            .ConfigureAwait(false);

        Update(state =>
        {
            state = ApplyRootProperties(state, root);
            state = ApplyPlayerProperties(state, player, isFullRead: true);
            return state;
        });

        await RefreshPositionAsync().ConfigureAwait(false);
    }

    /// <summary>Reads the Position property, which players do not report through PropertiesChanged.</summary>
    public async Task RefreshPositionAsync()
    {
        VariantValue value = await _client
            .GetPropertyAsync(ServiceName, MprisServices.ObjectPath, MprisServices.PlayerInterface, "Position")
            .ConfigureAwait(false);

        TimeSpan? position = VariantReader.AsDuration(value);
        if (position is null)
        {
            return;
        }

        Update(state => state with { Position = position, PositionSyncedAt = DateTimeOffset.UtcNow });
    }

    public Task<bool> PlayPauseAsync() => CallAsync("PlayPause");

    public Task<bool> PlayAsync() => CallAsync("Play");

    public Task<bool> PauseAsync() => CallAsync("Pause");

    public Task<bool> StopAsync() => CallAsync("Stop");

    public Task<bool> NextAsync() => CallAsync("Next");

    public Task<bool> PreviousAsync() => CallAsync("Previous");

    /// <summary>Seeks relative to the current position. A negative offset rewinds.</summary>
    public Task<bool> SeekAsync(TimeSpan offset)
    {
        long microseconds = offset.Ticks / 10;
        return _client.CallAsync(
            ServiceName,
            MprisServices.ObjectPath,
            MprisServices.PlayerInterface,
            "Seek",
            "x",
            (ref MessageWriter writer) => writer.WriteInt64(microseconds));
    }

    /// <summary>Jumps to an absolute position of the currently playing track.</summary>
    public Task<bool> SetPositionAsync(string trackId, TimeSpan position)
    {
        long microseconds = Math.Max(0, position.Ticks / 10);
        return _client.CallAsync(
            ServiceName,
            MprisServices.ObjectPath,
            MprisServices.PlayerInterface,
            "SetPosition",
            "ox",
            (ref MessageWriter writer) =>
            {
                writer.WriteObjectPath(trackId);
                writer.WriteInt64(microseconds);
            });
    }

    /// <summary>Sets the volume. MPRIS uses 0.0 to 1.0, values above 1.0 are left to the player.</summary>
    public Task<bool> SetVolumeAsync(double volume)
    {
        double clamped = Math.Clamp(volume, 0.0, 1.0);
        return _client.SetPropertyAsync(
            ServiceName,
            MprisServices.ObjectPath,
            MprisServices.PlayerInterface,
            "Volume",
            (ref MessageWriter writer) => writer.WriteVariantDouble(clamped));
    }

    public Task<bool> SetShuffleAsync(bool shuffle)
    {
        return _client.SetPropertyAsync(
            ServiceName,
            MprisServices.ObjectPath,
            MprisServices.PlayerInterface,
            "Shuffle",
            (ref MessageWriter writer) => writer.WriteVariantBool(shuffle));
    }

    /// <summary>Sets the MPRIS LoopStatus: None, Track or Playlist.</summary>
    public Task<bool> SetLoopStatusAsync(string loopStatus)
    {
        return _client.SetPropertyAsync(
            ServiceName,
            MprisServices.ObjectPath,
            MprisServices.PlayerInterface,
            "LoopStatus",
            (ref MessageWriter writer) => writer.WriteVariantString(loopStatus));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Changed = null;

        foreach (IDisposable watch in _watches)
        {
            watch.Dispose();
        }

        _watches.Clear();
    }

    private Task<bool> CallAsync(string member)
    {
        return _client.CallAsync(ServiceName, MprisServices.ObjectPath, MprisServices.PlayerInterface, member);
    }

    private async Task SubscribeAsync()
    {
        IDisposable? properties = await _client.WatchSignalAsync(
            ServiceName,
            MprisServices.ObjectPath,
            DBusClient.PropertiesInterface,
            "PropertiesChanged",
            ReadPropertiesChanged,
            OnPropertiesChanged).ConfigureAwait(false);

        IDisposable? seeked = await _client.WatchSignalAsync(
            ServiceName,
            MprisServices.ObjectPath,
            MprisServices.PlayerInterface,
            "Seeked",
            DBusClient.ReadInt64,
            OnSeeked).ConfigureAwait(false);

        Add(properties);
        Add(seeked);
    }

    private void Add(IDisposable? watch)
    {
        if (watch is null)
        {
            return;
        }

        if (_disposed)
        {
            watch.Dispose();
            return;
        }

        _watches.Add(watch);
    }

    private void OnPropertiesChanged(PropertiesChange change)
    {
        switch (change.Interface)
        {
            case MprisServices.RootInterface:
                Update(state => ApplyRootProperties(state, change.Changed));
                break;
            case MprisServices.PlayerInterface:
                Update(state => ApplyPlayerProperties(state, change.Changed, isFullRead: false));
                break;
        }
    }

    private void OnSeeked(long microseconds)
    {
        TimeSpan position = TimeSpan.FromTicks(microseconds * 10);
        Update(state => state with { Position = position, PositionSyncedAt = DateTimeOffset.UtcNow });
    }

    private static PlayerState ApplyRootProperties(PlayerState state, Dictionary<string, VariantValue> properties)
    {
        if (properties.Count == 0)
        {
            return state;
        }

        if (properties.TryGetValue("Identity", out VariantValue identity))
        {
            state = state with { Identity = VariantReader.AsString(identity) ?? state.Identity };
        }

        if (properties.TryGetValue("DesktopEntry", out VariantValue desktopEntry))
        {
            state = state with { DesktopEntry = VariantReader.AsString(desktopEntry) ?? state.DesktopEntry };
        }

        return state;
    }

    private static PlayerState ApplyPlayerProperties(
        PlayerState state,
        Dictionary<string, VariantValue> properties,
        bool isFullRead)
    {
        if (properties.Count == 0)
        {
            return state;
        }

        PlaybackStatus previousStatus = state.Status;
        string? previousTrackId = state.Metadata.TrackId;

        if (properties.TryGetValue("PlaybackStatus", out VariantValue status))
        {
            state = state with { Status = PlaybackStatusParser.Parse(VariantReader.AsString(status)) };
        }

        if (properties.TryGetValue("Metadata", out VariantValue metadata))
        {
            state = state with { Metadata = MediaMetadata.Parse(metadata) };
        }

        if (properties.TryGetValue("Volume", out VariantValue volume))
        {
            state = state with { Volume = VariantReader.AsDouble(volume) ?? state.Volume };
        }

        if (properties.TryGetValue("Shuffle", out VariantValue shuffle))
        {
            state = state with { Shuffle = VariantReader.AsBool(shuffle) ?? state.Shuffle };
        }

        if (properties.TryGetValue("LoopStatus", out VariantValue loopStatus))
        {
            state = state with { LoopStatus = VariantReader.AsString(loopStatus) ?? state.LoopStatus };
        }

        if (properties.TryGetValue("Position", out VariantValue position))
        {
            TimeSpan? value = VariantReader.AsDuration(position);
            if (value is not null)
            {
                state = state with { Position = value, PositionSyncedAt = DateTimeOffset.UtcNow };
            }
        }

        state = state with { Capabilities = ReadCapabilities(state.Capabilities, properties, isFullRead) };

        bool startedPlaying = state.Status == PlaybackStatus.Playing && previousStatus != PlaybackStatus.Playing;
        bool trackChanged = state.Metadata.TrackId is not null && state.Metadata.TrackId != previousTrackId;

        if (startedPlaying || trackChanged)
        {
            state = state with { LastActivity = DateTimeOffset.UtcNow };
        }

        // A status or track change invalidates the extrapolated position; the next read resyncs it.
        if (startedPlaying || trackChanged || previousStatus != state.Status)
        {
            state = state with { PositionSyncedAt = DateTimeOffset.UtcNow };

            if (trackChanged)
            {
                state = state with { Position = TimeSpan.Zero };
            }
        }

        return state;
    }

    private static PlayerCapabilities ReadCapabilities(
        PlayerCapabilities current,
        Dictionary<string, VariantValue> properties,
        bool isFullRead)
    {
        bool Read(string name, bool fallback)
        {
            return properties.TryGetValue(name, out VariantValue value)
                ? VariantReader.AsBool(value) ?? fallback
                : fallback;
        }

        // A property that is present at all means the player supports it; on a partial update the
        // absence of a property says nothing, so the known value is kept.
        bool hasVolume = properties.ContainsKey("Volume") || (!isFullRead && current.HasVolume);
        bool hasShuffle = properties.ContainsKey("Shuffle") || (!isFullRead && current.HasShuffle);
        bool hasLoopStatus = properties.ContainsKey("LoopStatus") || (!isFullRead && current.HasLoopStatus);

        return new PlayerCapabilities(
            CanControl: Read("CanControl", current.CanControl),
            CanPlay: Read("CanPlay", current.CanPlay),
            CanPause: Read("CanPause", current.CanPause),
            CanGoNext: Read("CanGoNext", current.CanGoNext),
            CanGoPrevious: Read("CanGoPrevious", current.CanGoPrevious),
            CanSeek: Read("CanSeek", current.CanSeek),
            HasVolume: hasVolume,
            HasShuffle: hasShuffle,
            HasLoopStatus: hasLoopStatus);
    }

    private void Update(Func<PlayerState, PlayerState> change)
    {
        PlayerState updated;

        lock (_gate)
        {
            updated = change(_state);
            if (updated == _state)
            {
                return;
            }

            _state = updated;
        }

        try
        {
            Changed?.Invoke(updated);
        }
        catch (Exception ex)
        {
            _logger.Error($"MPRIS: a state handler for {ServiceName} failed.", ex);
        }
    }

    private static PropertiesChange ReadPropertiesChanged(Message message, object? state)
    {
        Reader reader = message.GetBodyReader();
        string @interface = reader.ReadString();
        Dictionary<string, VariantValue> changed = reader.ReadDictionaryOfStringToVariantValue();
        string[] invalidated = reader.ReadArrayOfString();
        return new PropertiesChange(@interface, changed, invalidated);
    }
}

internal readonly record struct PropertiesChange(
    string Interface,
    Dictionary<string, VariantValue> Changed,
    string[] Invalidated);
