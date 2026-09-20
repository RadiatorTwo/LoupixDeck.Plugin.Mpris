namespace LoupixDeck.Plugin.Mpris;

/// <summary>The MPRIS PlaybackStatus values, plus Unknown for a player that did not report one.</summary>
internal enum PlaybackStatus
{
    Unknown,
    Playing,
    Paused,
    Stopped
}

internal static class PlaybackStatusParser
{
    public static PlaybackStatus Parse(string? value) => value switch
    {
        "Playing" => PlaybackStatus.Playing,
        "Paused" => PlaybackStatus.Paused,
        "Stopped" => PlaybackStatus.Stopped,
        _ => PlaybackStatus.Unknown
    };

    /// <summary>The English text shown on buttons. The host translates it through strings.&lt;code&gt;.json.</summary>
    public static string ToEnglishText(PlaybackStatus status) => status switch
    {
        PlaybackStatus.Playing => "Playing",
        PlaybackStatus.Paused => "Paused",
        PlaybackStatus.Stopped => "Stopped",
        _ => "Unknown"
    };
}
