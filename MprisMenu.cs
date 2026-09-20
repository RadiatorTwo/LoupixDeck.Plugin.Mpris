using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// Builds the command picker tree. The SDK has no dropdown for a parameter, so the strategy and
/// the fixed player are chosen by picking the matching menu entry, which pre-fills both values.
/// </summary>
internal sealed class MprisMenu(PlayerSelectionService selection)
{
    private static readonly PlayerStrategy[] Strategies =
    [
        PlayerStrategy.Automatic,
        PlayerStrategy.CurrentlyPlaying,
        PlayerStrategy.Preferred
    ];

    public IReadOnlyList<MenuNode> Build(ButtonTargets target)
    {
        bool includeRotary = target.HasFlag(ButtonTargets.RotaryEncoder);

        List<MenuNode> children = [];

        foreach (PlayerStrategy strategy in Strategies)
        {
            children.Add(new MenuNode
            {
                Name = PlayerStrategyParser.ToEnglishText(strategy),
                CommandName = string.Empty,
                Children = BuildActions(strategy, player: null, includeRotary)
            });
        }

        List<MenuNode> fixedPlayers = [];
        foreach (PlayerState player in selection.VisiblePlayers)
        {
            fixedPlayers.Add(new MenuNode
            {
                Name = player.DisplayName,
                CommandName = string.Empty,
                Children = BuildActions(PlayerStrategy.Fixed, player.ServiceName, includeRotary)
            });
        }

        if (fixedPlayers.Count > 0)
        {
            children.Add(new MenuNode
            {
                Name = PlayerStrategyParser.ToEnglishText(PlayerStrategy.Fixed),
                CommandName = string.Empty,
                Children = fixedPlayers
            });
        }

        children.Add(new MenuNode { Name = "Select Player", CommandName = "Mpris.SelectPlayer" });

        return
        [
            new MenuNode { Name = "Media", CommandName = string.Empty, Children = children }
        ];
    }

    private List<MenuNode> BuildActions(PlayerStrategy strategy, string? player, bool includeRotary)
    {
        Dictionary<string, string> Parameters() => MprisParameters.Build(strategy, player);

        List<MenuNode> actions = [];

        if (includeRotary)
        {
            actions.Add(new MenuNode
            {
                Name = "Volume Control",
                RotaryGroup = new Dictionary<RotaryAction, MenuCommandRef>
                {
                    [RotaryAction.CounterClockwise] = new() { CommandName = "Mpris.VolumeDown", Parameters = Parameters() },
                    [RotaryAction.Clockwise] = new() { CommandName = "Mpris.VolumeUp", Parameters = Parameters() },
                    [RotaryAction.Press] = new() { CommandName = "Mpris.PlayPause", Parameters = Parameters() }
                }
            });

            actions.Add(new MenuNode
            {
                Name = "Seek Control",
                RotaryGroup = new Dictionary<RotaryAction, MenuCommandRef>
                {
                    [RotaryAction.CounterClockwise] = new() { CommandName = "Mpris.SeekBackward", Parameters = Parameters() },
                    [RotaryAction.Clockwise] = new() { CommandName = "Mpris.SeekForward", Parameters = Parameters() },
                    [RotaryAction.Press] = new() { CommandName = "Mpris.PlayPause", Parameters = Parameters() }
                }
            });
        }

        foreach ((string name, string command) in Actions)
        {
            actions.Add(new MenuNode { Name = name, CommandName = command, Parameters = Parameters() });
        }

        actions.Add(new MenuNode
        {
            Name = "Displays",
            CommandName = string.Empty,
            Children = [.. Displays.Select(entry => new MenuNode
            {
                Name = entry.Name,
                CommandName = entry.Command,
                Parameters = Parameters()
            })]
        });

        return actions;
    }

    private static readonly (string Name, string Command)[] Actions =
    [
        ("Play/Pause", "Mpris.PlayPause"),
        ("Play", "Mpris.Play"),
        ("Pause", "Mpris.Pause"),
        ("Stop", "Mpris.Stop"),
        ("Next", "Mpris.Next"),
        ("Previous", "Mpris.Previous"),
        ("Seek Forward", "Mpris.SeekForward"),
        ("Seek Backward", "Mpris.SeekBackward"),
        ("Seek To Position", "Mpris.SeekToPosition"),
        ("Volume Up", "Mpris.VolumeUp"),
        ("Volume Down", "Mpris.VolumeDown"),
        ("Set Volume", "Mpris.SetVolume"),
        ("Shuffle", "Mpris.ToggleShuffle"),
        ("Repeat", "Mpris.CycleRepeat"),
        ("Set As Preferred Player", "Mpris.SetPreferredPlayer")
    ];

    private static readonly (string Name, string Command)[] Displays =
    [
        ("Now Playing", "Mpris.NowPlaying"),
        ("Artwork", "Mpris.Artwork"),
        ("Player Name", "Mpris.PlayerName"),
        ("Playback Status", "Mpris.Status"),
        ("Title", "Mpris.Title"),
        ("Artist", "Mpris.Artist"),
        ("Album", "Mpris.Album"),
        ("Position", "Mpris.Position"),
        ("Duration", "Mpris.Duration"),
        ("Progress", "Mpris.Progress"),
        ("Volume", "Mpris.Volume"),
        ("Shuffle State", "Mpris.Shuffle"),
        ("Repeat Mode", "Mpris.Repeat"),
        ("Player Available", "Mpris.PlayerAvailable")
    ];
}
