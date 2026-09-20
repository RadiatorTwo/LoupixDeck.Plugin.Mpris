namespace LoupixDeck.Plugin.Mpris;

/// <summary>How a command decides which player it acts on.</summary>
internal enum PlayerStrategy
{
    /// <summary>The most recently active player.</summary>
    Automatic,

    /// <summary>The first player that is playing right now.</summary>
    CurrentlyPlaying,

    /// <summary>The player selected globally in the plugin settings.</summary>
    Preferred,

    /// <summary>The player stored in the command's own parameters.</summary>
    Fixed
}

internal static class PlayerStrategyParser
{
    public const string AutomaticValue = "auto";
    public const string CurrentlyPlayingValue = "playing";
    public const string PreferredValue = "preferred";
    public const string FixedValue = "fixed";

    /// <summary>
    /// Reads the strategy parameter. Anything unknown, including an empty value from a binding
    /// saved before this parameter existed, means Automatic.
    /// </summary>
    public static PlayerStrategy Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PlayerStrategy.Automatic;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            CurrentlyPlayingValue => PlayerStrategy.CurrentlyPlaying,
            PreferredValue => PlayerStrategy.Preferred,
            FixedValue => PlayerStrategy.Fixed,
            _ => PlayerStrategy.Automatic
        };
    }

    public static string ToParameterValue(PlayerStrategy strategy) => strategy switch
    {
        PlayerStrategy.CurrentlyPlaying => CurrentlyPlayingValue,
        PlayerStrategy.Preferred => PreferredValue,
        PlayerStrategy.Fixed => FixedValue,
        _ => AutomaticValue
    };

    /// <summary>The English menu label. The host translates it through strings.&lt;code&gt;.json.</summary>
    public static string ToEnglishText(PlayerStrategy strategy) => strategy switch
    {
        PlayerStrategy.CurrentlyPlaying => "Currently Playing",
        PlayerStrategy.Preferred => "Preferred Player",
        PlayerStrategy.Fixed => "Fixed Player",
        _ => "Automatic"
    };
}
