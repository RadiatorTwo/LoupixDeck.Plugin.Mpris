using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>Seeks forward or backward by a configurable step.</summary>
internal sealed class MprisSeekCommand : MprisCommandBase
{
    private readonly MprisSettings _settings;
    private readonly PositionTracker _positions;
    private readonly int _sign;

    public MprisSeekCommand(
        PlayerSelectionService selection,
        MprisSettings settings,
        PositionTracker positions,
        IPluginLogger logger,
        bool forward)
        : base(selection, logger)
    {
        _settings = settings;
        _positions = positions;
        _sign = forward ? 1 : -1;

        Descriptor = new CommandDescriptor
        {
            CommandName = forward ? "Mpris.SeekForward" : "Mpris.SeekBackward",
            DisplayName = forward ? "Media: Seek Forward" : "Media: Seek Backward",
            Group = "Media",
            Icon = forward ? "\U000F0211" : "\U000F0213",
            Description = forward
                ? "Jump forward in the current track"
                : "Jump backward in the current track",
            ParameterTemplate = MprisParameters.SecondsTemplate,
            Parameters = MprisParameters.SeekParameters
        };
    }

    public override CommandDescriptor Descriptor { get; }

    protected override async Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        if (!Supports(player, player.State.Capabilities.CanSeek, "seeking"))
        {
            return;
        }

        int seconds = Math.Abs(MprisParameters.ReadNumber(ctx, 2, _settings.SeekStepSeconds));
        TimeSpan offset = TimeSpan.FromSeconds(seconds * _sign);

        if (!await player.SeekAsync(offset).ConfigureAwait(false))
        {
            return;
        }

        // Seek does not always emit Seeked, so the position is read back for the displays.
        await player.RefreshPositionAsync().ConfigureAwait(false);
        MprisParameters.ShowOverlay(ctx, MprisDisplayCommands.FormatTime(_positions.GetPosition(player.State)));
    }
}

/// <summary>Jumps to an absolute position of the current track.</summary>
internal sealed class MprisSeekToPositionCommand(
    PlayerSelectionService selection,
    PositionTracker positions,
    IPluginLogger logger)
    : MprisCommandBase(selection, logger)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "Mpris.SeekToPosition",
        DisplayName = "Media: Seek To Position",
        Group = "Media",
        Icon = "\U000F0954",
        Description = "Jump to a position of the current track, in seconds",
        ParameterTemplate = MprisParameters.PositionTemplate,
        Parameters = MprisParameters.PositionParameters
    };

    protected override async Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        PlayerState state = player.State;

        if (!Supports(player, state.Capabilities.CanSeek, "seeking"))
        {
            return;
        }

        if (state.Metadata.TrackId is not { } trackId)
        {
            // SetPosition is bound to a track id; without one the absolute jump cannot be sent.
            Logger.Info($"MPRIS: {player.ServiceName} reports no track id, the position was not changed.");
            return;
        }

        TimeSpan position = TimeSpan.FromSeconds(Math.Max(0, MprisParameters.ReadNumber(ctx, 2, 0)));
        if (state.Duration is { } duration && position > duration)
        {
            position = duration;
        }

        if (!await player.SetPositionAsync(trackId, position).ConfigureAwait(false))
        {
            return;
        }

        await player.RefreshPositionAsync().ConfigureAwait(false);
        MprisParameters.ShowOverlay(ctx, MprisDisplayCommands.FormatTime(positions.GetPosition(player.State)));
    }
}
