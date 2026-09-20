using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// Typed access to the plugin settings. The host stores them as JSON, so numbers come back as
/// long and every read needs a default for a file written by an older version.
/// </summary>
internal sealed class MprisSettings(IPluginSettings settings)
{
    public const string PreferredPlayerKey = "preferredPlayer";
    public const string AutomaticSelectionKey = "automaticSelection";
    public const string ShowBrowserPlayersKey = "showBrowserPlayers";
    public const string HttpArtworkKey = "httpArtwork";
    public const string SeekStepSecondsKey = "seekStepSeconds";
    public const string VolumeStepPercentKey = "volumeStepPercent";
    public const string FallbackWhenUnavailableKey = "fallbackWhenUnavailable";

    public const int DefaultSeekStepSeconds = 10;
    public const int DefaultVolumeStepPercent = 5;

    /// <summary>Bus name or application name of the player the Preferred Player strategy uses.</summary>
    public string PreferredPlayer
    {
        get => settings.Get<string>(PreferredPlayerKey, string.Empty) ?? string.Empty;
        set
        {
            settings.Set(PreferredPlayerKey, value);
            settings.Save();
        }
    }

    /// <summary>When off, the Automatic strategy resolves to nothing instead of guessing a player.</summary>
    public bool AutomaticSelection => settings.Get(AutomaticSelectionKey, true);

    public bool ShowBrowserPlayers => settings.Get(ShowBrowserPlayersKey, true);

    public bool HttpArtwork => settings.Get(HttpArtworkKey, true);

    /// <summary>Fall back to the automatic player when the selected one is not running.</summary>
    public bool FallbackWhenUnavailable => settings.Get(FallbackWhenUnavailableKey, false);

    public int SeekStepSeconds => ReadPositiveNumber(SeekStepSecondsKey, DefaultSeekStepSeconds, 1, 600);

    public int VolumeStepPercent => ReadPositiveNumber(VolumeStepPercentKey, DefaultVolumeStepPercent, 1, 50);

    private int ReadPositiveNumber(string key, int fallback, int minimum, int maximum)
    {
        long value = settings.Get(key, (long)fallback);
        return value <= 0 ? fallback : (int)Math.Clamp(value, minimum, maximum);
    }
}
