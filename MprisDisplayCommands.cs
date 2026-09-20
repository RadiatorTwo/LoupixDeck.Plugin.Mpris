using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// A touch button that shows one value of the selected player and toggles playback when pressed.
/// The text comes from the cached registry state, so GetText never touches D-Bus.
/// </summary>
internal sealed class MprisTextDisplayCommand : MprisCommandBase, IDisplayCommand
{
    private readonly Func<PlayerState?, IPluginHost, string> _text;

    public MprisTextDisplayCommand(
        PlayerSelectionService selection,
        IPluginLogger logger,
        string commandName,
        string displayName,
        string description,
        string icon,
        Func<PlayerState?, IPluginHost, string> text)
        : base(selection, logger)
    {
        _text = text;
        Descriptor = new CommandDescriptor
        {
            CommandName = commandName,
            DisplayName = displayName,
            Group = "Media",
            Icon = icon,
            Description = description,
            ParameterTemplate = MprisParameters.PlayerTemplate,
            Parameters = MprisParameters.PlayerParameters
        };
    }

    public override CommandDescriptor Descriptor { get; }

    public override ButtonTargets SupportedTargets => ButtonTargets.TouchButton;

    /// <summary>
    /// The registry pushes a refresh whenever a player changes, so this interval only covers the
    /// values that move on their own, above all the extrapolated position.
    /// </summary>
    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

    public string GetText(CommandContext ctx)
    {
        try
        {
            PlayerState? state = Selection.Resolve(MprisParameters.ReadStrategy(ctx), MprisParameters.ReadPlayer(ctx));
            return _text(state, ctx.Host);
        }
        catch (Exception ex)
        {
            Logger.Error($"MPRIS: {Descriptor.CommandName} could not build its text.", ex);
            return string.Empty;
        }
    }

    /// <summary>Pressing a display button toggles playback of the player it shows.</summary>
    protected override Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        PlayerState state = player.State;
        bool capability = state.Status == PlaybackStatus.Playing
            ? state.Capabilities.CanPause
            : state.Capabilities.CanPlay;

        return Supports(player, capability, "play/pause") ? player.PlayPauseAsync() : Task.CompletedTask;
    }
}

/// <summary>Builds the display commands and formats the values they show.</summary>
internal static class MprisDisplayCommands
{
    public static IEnumerable<IPluginCommand> Create(
        PlayerSelectionService selection,
        PositionTracker positions,
        IPluginLogger logger)
    {
        yield return new MprisTextDisplayCommand(
            selection, logger,
            "Mpris.PlayerName", "Media: Player Name", "Show the name of the selected player", "\U000F075A",
            (state, host) => state is null ? NoPlayer(host) : state.DisplayName);

        yield return new MprisTextDisplayCommand(
            selection, logger,
            "Mpris.Status", "Media: Playback Status", "Show whether the player is playing, paused or stopped", "\U000F040A",
            (state, host) => host.Tr(PlaybackStatusParser.ToEnglishText(state?.Status ?? PlaybackStatus.Unknown)));

        yield return new MprisTextDisplayCommand(
            selection, logger,
            "Mpris.Title", "Media: Title", "Show the title of the current track", "\U000F075A",
            (state, host) => state?.Metadata.Title ?? NoPlayer(host, state));

        yield return new MprisTextDisplayCommand(
            selection, logger,
            "Mpris.Artist", "Media: Artist", "Show the artist of the current track", "\U000F0004",
            (state, host) => state?.Metadata.Artist ?? NoPlayer(host, state));

        yield return new MprisTextDisplayCommand(
            selection, logger,
            "Mpris.Album", "Media: Album", "Show the album of the current track", "\U000F02E9",
            (state, host) => state?.Metadata.Album ?? NoPlayer(host, state));

        yield return new MprisTextDisplayCommand(
            selection, logger,
            "Mpris.NowPlaying", "Media: Now Playing", "Show status, artist, title and position of the selected player", "\U000F075A",
            (state, host) => FormatNowPlaying(state, host, positions));

        yield return new MprisTextDisplayCommand(
            selection, logger,
            "Mpris.Position", "Media: Position", "Show the position and duration of the current track", "\U000F0954",
            (state, host) => state is null ? NoPlayer(host) : FormatPositionLine(state, positions));

        yield return new MprisTextDisplayCommand(
            selection, logger,
            "Mpris.Duration", "Media: Duration", "Show the length of the current track", "\U000F0954",
            (state, host) => state?.Duration is { } duration ? FormatTime(duration) : NoPlayer(host, state));

        yield return new MprisTextDisplayCommand(
            selection, logger,
            "Mpris.Progress", "Media: Progress", "Show how far the current track has played, in percent", "\U000F0954",
            (state, host) => state is null ? NoPlayer(host) : FormatProgress(state, positions));

        yield return new MprisTextDisplayCommand(
            selection, logger,
            "Mpris.Volume", "Media: Volume", "Show the volume of the selected player", "\U000F057E",
            (state, host) => state?.Volume is { } volume && state.Capabilities.HasVolume
                ? FormatVolume(volume)
                : NoPlayer(host, state));

        yield return new MprisTextDisplayCommand(
            selection, logger,
            "Mpris.Shuffle", "Media: Shuffle", "Show whether shuffle is on", "\U000F049D",
            (state, host) => state?.Shuffle is { } shuffle && state.Capabilities.HasShuffle
                ? host.Tr(shuffle ? "Shuffle on" : "Shuffle off")
                : NoPlayer(host, state));

        yield return new MprisTextDisplayCommand(
            selection, logger,
            "Mpris.Repeat", "Media: Repeat", "Show the repeat mode", "\U000F0456",
            (state, host) => state?.LoopStatus is { } loop && state.Capabilities.HasLoopStatus
                ? host.Tr(FormatLoopStatus(loop))
                : NoPlayer(host, state));

        yield return new MprisTextDisplayCommand(
            selection, logger,
            "Mpris.PlayerAvailable", "Media: Player Available", "Show whether the selected player is running", "\U000F05A9",
            (state, host) => host.Tr(state is null ? "Offline" : "Available"));
    }

    /// <summary>The three-line label from the issue: status and artist, title, position.</summary>
    public static string FormatNowPlaying(PlayerState? state, IPluginHost host, PositionTracker positions)
    {
        if (state is null)
        {
            return NoPlayer(host);
        }

        List<string> lines = [];

        string header = StatusGlyph(state.Status);
        if (state.Metadata.Artist is { } artist)
        {
            header = $"{header} {artist}";
        }
        else
        {
            header = $"{header} {state.DisplayName}";
        }

        lines.Add(header);

        if (state.Metadata.Title is { } title)
        {
            lines.Add(title);
        }

        if (state.Duration is not null)
        {
            lines.Add(FormatPositionLine(state, positions));
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatPositionLine(PlayerState state, PositionTracker positions)
    {
        TimeSpan position = positions.GetPosition(state);
        return state.Duration is { } duration
            ? $"{FormatTime(position)} / {FormatTime(duration)}"
            : FormatTime(position);
    }

    public static string FormatProgress(PlayerState state, PositionTracker positions)
    {
        double? progress = positions.GetProgress(state);
        return progress is null ? "-" : $"{(int)Math.Round(progress.Value * 100)}%";
    }

    public static string FormatVolume(double volume) => $"{(int)Math.Round(Math.Clamp(volume, 0, 1) * 100)}%";

    public static string FormatTime(TimeSpan value)
    {
        TimeSpan time = value < TimeSpan.Zero ? TimeSpan.Zero : value;
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

    /// <summary>Maps the raw MPRIS LoopStatus to the English text shown on a button.</summary>
    public static string FormatLoopStatus(string loopStatus) => loopStatus switch
    {
        "Track" => "Repeat track",
        "Playlist" => "Repeat playlist",
        _ => "Repeat off"
    };

    public static string StatusGlyph(PlaybackStatus status) => status switch
    {
        PlaybackStatus.Playing => "▶",
        PlaybackStatus.Paused => "⏸",
        PlaybackStatus.Stopped => "⏹",
        _ => "–"
    };

    private static string NoPlayer(IPluginHost host) => host.Tr("No player");

    /// <summary>"No player" when nothing is selected, an empty label when the value is simply missing.</summary>
    private static string NoPlayer(IPluginHost host, PlayerState? state) => state is null ? NoPlayer(host) : string.Empty;
}
