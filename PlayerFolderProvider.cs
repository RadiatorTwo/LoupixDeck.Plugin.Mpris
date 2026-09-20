using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// The touch-screen folder that lists the running players. Pressing an entry makes that player
/// the preferred one. The back slot is reserved by the host, so it is skipped here.
/// </summary>
internal sealed class PlayerFolderProvider(
    PlayerRegistry registry,
    PlayerSelectionService selection,
    MprisSettings settings,
    PositionTracker positions,
    IPluginHost host)
    : FolderProviderBase
{
    private static readonly PluginColor PlayingColor = PluginColor.FromRgb(20, 80, 40);
    private static readonly PluginColor PausedColor = PluginColor.FromRgb(70, 70, 70);
    private static readonly PluginColor StoppedColor = PluginColor.FromRgb(40, 40, 40);
    private static readonly PluginColor PreferredColor = PluginColor.FromRgb(20, 60, 110);

    public override string Title => host.Tr("Media Players");

    public override IReadOnlyList<FolderEntry> BuildEntries()
    {
        FolderGridInfo grid = host.FolderGrid;
        IReadOnlyList<PlayerState> players = selection.VisiblePlayers;

        if (players.Count == 0)
        {
            return
            [
                new FolderEntry
                {
                    SlotIndex = grid.SlotForIndex(0),
                    Text = host.Tr("No player"),
                    BackColor = StoppedColor,
                    TextSize = 14
                }
            ];
        }

        List<FolderEntry> entries = [];
        string preferred = settings.PreferredPlayer;

        for (int index = 0; index < players.Count; index++)
        {
            int slot = grid.SlotForIndex(index);
            if (slot < 0)
            {
                // The grid is full; the remaining players stay reachable through the command menu.
                break;
            }

            PlayerState player = players[index];
            bool isPreferred = string.Equals(player.ServiceName, preferred, StringComparison.Ordinal);

            entries.Add(new FolderEntry
            {
                SlotIndex = slot,
                Text = BuildText(player),
                BackColor = isPreferred ? PreferredColor : StatusColor(player.Status),
                TextSize = 13,
                Bold = isPreferred,
                OnPress = () => SelectAsync(player.ServiceName)
            });
        }

        return entries;
    }

    public override void OnEnter()
    {
        registry.PlayersChanged += RaiseEntriesChanged;
        registry.PlayerUpdated += OnPlayerUpdated;
    }

    public override void OnExit()
    {
        registry.PlayersChanged -= RaiseEntriesChanged;
        registry.PlayerUpdated -= OnPlayerUpdated;
    }

    private void OnPlayerUpdated(PlayerState state) => RaiseEntriesChanged();

    private Task SelectAsync(string serviceName)
    {
        settings.PreferredPlayer = serviceName;
        RaiseEntriesChanged();
        return Task.CompletedTask;
    }

    private string BuildText(PlayerState player)
    {
        string status = host.Tr(PlaybackStatusParser.ToEnglishText(player.Status));
        string title = player.Metadata.Title ?? string.Empty;
        string position = player.Duration is null
            ? string.Empty
            : MprisDisplayCommands.FormatPositionLine(player, positions);

        string[] lines =
        [
            player.DisplayName,
            $"{MprisDisplayCommands.StatusGlyph(player.Status)} {status}",
            title,
            position
        ];

        return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static PluginColor StatusColor(PlaybackStatus status) => status switch
    {
        PlaybackStatus.Playing => PlayingColor,
        PlaybackStatus.Paused => PausedColor,
        _ => StoppedColor
    };
}
