using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// Provides the playback position without polling D-Bus. While a player is playing the position
/// is extrapolated from the last known value; a resync only happens when a position is actually
/// being shown, and then at most every ten seconds.
/// </summary>
internal sealed class PositionTracker : IDisposable
{
    private static readonly TimeSpan ResyncInterval = TimeSpan.FromSeconds(10);

    /// <summary>How long after the last read a position still counts as being shown.</summary>
    private static readonly TimeSpan DisplayTimeout = TimeSpan.FromSeconds(30);

    private readonly PlayerRegistry _registry;
    private readonly IPluginLogger _logger;
    private readonly Timer _timer;

    private long _lastReadTicks;
    private bool _disposed;

    public PositionTracker(PlayerRegistry registry, IPluginLogger logger)
    {
        _registry = registry;
        _logger = logger;
        _timer = new Timer(_ => Resync(), null, ResyncInterval, ResyncInterval);
    }

    /// <summary>
    /// The current position: the value the player last reported plus the time that has passed
    /// since, while it is playing.
    /// </summary>
    public TimeSpan GetPosition(PlayerState state)
    {
        Interlocked.Exchange(ref _lastReadTicks, DateTimeOffset.UtcNow.Ticks);

        TimeSpan position = state.Position ?? TimeSpan.Zero;

        if (state.Status == PlaybackStatus.Playing && state.PositionSyncedAt > DateTimeOffset.MinValue)
        {
            position += DateTimeOffset.UtcNow - state.PositionSyncedAt;
        }

        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        // Extrapolation must not run past the end of the track while the next state is on its way.
        return state.Duration is { } duration && position > duration ? duration : position;
    }

    /// <summary>The progress from 0.0 to 1.0, or null when the track has no known length.</summary>
    public double? GetProgress(PlayerState state)
    {
        if (state.Duration is not { } duration || duration <= TimeSpan.Zero)
        {
            return null;
        }

        return Math.Clamp(GetPosition(state) / duration, 0.0, 1.0);
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
    }

    /// <summary>Re-reads the position of the playing players, but only while one is being shown.</summary>
    private void Resync()
    {
        if (_disposed)
        {
            return;
        }

        DateTimeOffset lastRead = new(Interlocked.Read(ref _lastReadTicks), TimeSpan.Zero);
        if (DateTimeOffset.UtcNow - lastRead > DisplayTimeout)
        {
            return;
        }

        foreach (PlayerState state in _registry.Players)
        {
            if (state.Status != PlaybackStatus.Playing)
            {
                continue;
            }

            PlayerProxy? proxy = _registry.GetProxy(state.ServiceName);
            if (proxy is null)
            {
                continue;
            }

            _ = RefreshAsync(proxy);
        }
    }

    private async Task RefreshAsync(PlayerProxy proxy)
    {
        try
        {
            await proxy.RefreshPositionAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Warn($"MPRIS: cannot read the position of {proxy.ServiceName}: {ex.Message}");
        }
    }
}
