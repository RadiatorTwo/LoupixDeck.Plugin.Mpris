namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// A snapshot of one MPRIS player. Instances are immutable so a display command can read a
/// consistent state without locking while the D-Bus read loop publishes the next one.
/// </summary>
internal sealed record PlayerState
{
    public required string ServiceName { get; init; }

    /// <summary>The player's own name (org.mpris.MediaPlayer2.Identity), e.g. "VLC media player".</summary>
    public string Identity { get; init; } = "";

    public string? DesktopEntry { get; init; }

    public PlaybackStatus Status { get; init; } = PlaybackStatus.Unknown;

    public MediaMetadata Metadata { get; init; } = MediaMetadata.Empty;

    /// <summary>The position as last read from the player. Extrapolation happens in <see cref="PositionTracker"/>.</summary>
    public TimeSpan? Position { get; init; }

    /// <summary>When <see cref="Position"/> was read, used to extrapolate without polling D-Bus.</summary>
    public DateTimeOffset PositionSyncedAt { get; init; } = DateTimeOffset.MinValue;

    public double? Volume { get; init; }

    public bool? Shuffle { get; init; }

    /// <summary>The raw MPRIS LoopStatus: None, Track or Playlist.</summary>
    public string? LoopStatus { get; init; }

    public PlayerCapabilities Capabilities { get; init; } = PlayerCapabilities.Unknown;

    /// <summary>Last time this player started playing or changed track. Breaks ties between players.</summary>
    public DateTimeOffset LastActivity { get; init; } = DateTimeOffset.MinValue;

    public TimeSpan? Duration => Metadata.Length;

    /// <summary>The name shown to the user; falls back to the bus name when the player reports no identity.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Identity)
        ? MprisServices.GetApplicationName(ServiceName)
        : Identity;

    /// <summary>The stable part of the bus name, used to match players across restarts.</summary>
    public string ApplicationName => MprisServices.GetApplicationName(ServiceName);
}
