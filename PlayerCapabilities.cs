namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// What a player says it supports. An action the player does not support is skipped with a log
/// entry instead of being sent and failing, as the issue requires.
/// </summary>
internal readonly record struct PlayerCapabilities(
    bool CanControl,
    bool CanPlay,
    bool CanPause,
    bool CanGoNext,
    bool CanGoPrevious,
    bool CanSeek,
    bool HasVolume,
    bool HasShuffle,
    bool HasLoopStatus)
{
    /// <summary>
    /// The state of a player that has not answered yet. Control is assumed so a command issued
    /// before the first property read is not dropped; the player itself still rejects it if it
    /// really cannot be controlled.
    /// </summary>
    public static readonly PlayerCapabilities Unknown = new(
        CanControl: true,
        CanPlay: true,
        CanPause: true,
        CanGoNext: true,
        CanGoPrevious: true,
        CanSeek: true,
        HasVolume: false,
        HasShuffle: false,
        HasLoopStatus: false);
}
