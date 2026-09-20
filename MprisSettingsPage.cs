using System.Globalization;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// The plugin's settings form. The SDK renders the descriptors itself, so the plugin only
/// declares which values exist. Labels are English keys the host translates.
/// </summary>
internal sealed class MprisSettingsPage(
    PlayerRegistry registry,
    PlayerSelectionService selection,
    MprisSettings settings,
    IPluginHost host)
{
    public IReadOnlyList<PluginSettingDescriptor> BuildSchema() =>
    [
        new PluginSettingDescriptor
        {
            Key = "playerHeading",
            Label = "Player selection",
            Kind = PluginSettingKind.Heading
        },
        new PluginSettingDescriptor
        {
            Key = MprisSettings.PreferredPlayerKey,
            Label = "Preferred player",
            Kind = PluginSettingKind.Text,
            Description = "Bus name of the player the Preferred Player strategy uses, for example org.mpris.MediaPlayer2.vlc. The player folder fills this in when an entry is pressed.",
            DefaultValue = ""
        },
        new PluginSettingDescriptor
        {
            Key = MprisSettings.AutomaticSelectionKey,
            Label = "Automatic player selection",
            Kind = PluginSettingKind.Toggle,
            Description = "Let the Automatic strategy pick the most recently active player. When off, only an explicitly selected player is controlled.",
            DefaultValue = true
        },
        new PluginSettingDescriptor
        {
            Key = MprisSettings.FallbackWhenUnavailableKey,
            Label = "Fall back when the selected player is unavailable",
            Kind = PluginSettingKind.Toggle,
            Description = "When the fixed or preferred player is not running, act on the most recently active player instead of doing nothing.",
            DefaultValue = false
        },
        new PluginSettingDescriptor
        {
            Key = MprisSettings.ShowBrowserPlayersKey,
            Label = "Show browser players",
            Kind = PluginSettingKind.Toggle,
            Description = "List players provided by web browsers.",
            DefaultValue = true
        },
        new PluginSettingDescriptor
        {
            Key = "stepHeading",
            Label = "Steps",
            Kind = PluginSettingKind.Heading
        },
        new PluginSettingDescriptor
        {
            Key = MprisSettings.SeekStepSecondsKey,
            Label = "Seek step in seconds",
            Kind = PluginSettingKind.Number,
            Description = "Used by the seek commands that do not carry their own step.",
            DefaultValue = MprisSettings.DefaultSeekStepSeconds
        },
        new PluginSettingDescriptor
        {
            Key = MprisSettings.VolumeStepPercentKey,
            Label = "Volume step in percent",
            Kind = PluginSettingKind.Number,
            Description = "Used by the volume commands that do not carry their own step.",
            DefaultValue = MprisSettings.DefaultVolumeStepPercent
        },
        new PluginSettingDescriptor
        {
            Key = "artworkHeading",
            Label = "Artwork",
            Kind = PluginSettingKind.Heading
        },
        new PluginSettingDescriptor
        {
            Key = MprisSettings.HttpArtworkKey,
            Label = "Load artwork from the internet",
            Kind = PluginSettingKind.Toggle,
            Description = "Download cover art that a player publishes as an http address. Local cover files are always used.",
            DefaultValue = false
        }
    ];

    public IReadOnlyList<PluginSettingAction> BuildActions() =>
    [
        new PluginSettingAction
        {
            Label = "List running players",
            Invoke = () => Task.FromResult(DescribePlayers())
        },
        new PluginSettingAction
        {
            Label = "Clear the preferred player",
            Invoke = () =>
            {
                settings.PreferredPlayer = string.Empty;
                return Task.FromResult(host.Tr("The preferred player was cleared."));
            }
        }
    ];

    /// <summary>The action result is built while running, so the plugin translates it itself.</summary>
    private string DescribePlayers()
    {
        IReadOnlyList<PlayerState> players = selection.VisiblePlayers;

        if (players.Count == 0)
        {
            return host.Tr("No player is running.");
        }

        IEnumerable<string> lines = players.Select(player =>
            $"{player.DisplayName} ({player.ServiceName}) - {host.Tr(PlaybackStatusParser.ToEnglishText(player.Status))}");

        string header = string.Format(
            CultureInfo.CurrentCulture,
            host.Tr("{0} player(s) running:"),
            registry.Players.Count);

        return string.Join(Environment.NewLine, lines.Prepend(header));
    }
}
