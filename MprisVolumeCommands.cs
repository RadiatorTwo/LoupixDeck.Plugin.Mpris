using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>Raises or lowers the volume of a player that supports it.</summary>
internal sealed class MprisVolumeStepCommand : MprisCommandBase
{
    private readonly MprisSettings _settings;
    private readonly int _sign;

    public MprisVolumeStepCommand(
        PlayerSelectionService selection,
        MprisSettings settings,
        IPluginLogger logger,
        bool up)
        : base(selection, logger)
    {
        _settings = settings;
        _sign = up ? 1 : -1;

        Descriptor = new CommandDescriptor
        {
            CommandName = up ? "Mpris.VolumeUp" : "Mpris.VolumeDown",
            DisplayName = up ? "Media: Volume Up" : "Media: Volume Down",
            Group = "Media",
            Icon = up ? "\U000F057E" : "\U000F0580",
            Description = up
                ? "Raise the volume of the selected player"
                : "Lower the volume of the selected player",
            ParameterTemplate = MprisParameters.StepTemplate,
            Parameters = MprisParameters.VolumeStepParameters
        };
    }

    public override CommandDescriptor Descriptor { get; }

    protected override async Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        PlayerState state = player.State;

        if (!Supports(player, state.Capabilities.HasVolume, "volume control"))
        {
            return;
        }

        int step = MprisParameters.ReadStep(ctx, 2, _settings.VolumeStepPercent);
        double current = state.Volume ?? 0.0;
        double next = Math.Clamp(current + (step * _sign / 100.0), 0.0, 1.0);

        if (await player.SetVolumeAsync(next).ConfigureAwait(false))
        {
            MprisParameters.ShowOverlay(ctx, MprisDisplayCommands.FormatVolume(next));
        }
    }
}

/// <summary>Sets the volume to an absolute level in percent.</summary>
internal sealed class MprisSetVolumeCommand(PlayerSelectionService selection, IPluginLogger logger)
    : MprisCommandBase(selection, logger)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "Mpris.SetVolume",
        DisplayName = "Media: Set Volume",
        Group = "Media",
        Icon = "\U000F057F",
        Description = "Set the volume of the selected player, in percent",
        ParameterTemplate = MprisParameters.PercentTemplate,
        Parameters = MprisParameters.VolumePercentParameters
    };

    protected override async Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        if (!Supports(player, player.State.Capabilities.HasVolume, "volume control"))
        {
            return;
        }

        double target = Math.Clamp(MprisParameters.ReadNumber(ctx, 2, 50), 0, 100) / 100.0;

        if (await player.SetVolumeAsync(target).ConfigureAwait(false))
        {
            MprisParameters.ShowOverlay(ctx, MprisDisplayCommands.FormatVolume(target));
        }
    }
}

/// <summary>Turns shuffle on or off.</summary>
internal sealed class MprisToggleShuffleCommand(PlayerSelectionService selection, IPluginLogger logger)
    : MprisCommandBase(selection, logger)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "Mpris.ToggleShuffle",
        DisplayName = "Media: Toggle Shuffle",
        Group = "Media",
        Icon = "\U000F049D",
        Description = "Turn shuffle on or off",
        ParameterTemplate = MprisParameters.PlayerTemplate,
        Parameters = MprisParameters.PlayerParameters
    };

    protected override async Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        PlayerState state = player.State;

        if (!Supports(player, state.Capabilities.HasShuffle, "shuffle"))
        {
            return;
        }

        bool next = !(state.Shuffle ?? false);

        if (await player.SetShuffleAsync(next).ConfigureAwait(false))
        {
            MprisParameters.ShowOverlay(ctx, ctx.Host.Tr(next ? "Shuffle on" : "Shuffle off"));
        }
    }
}

/// <summary>Cycles the repeat mode: off, track, playlist.</summary>
internal sealed class MprisCycleRepeatCommand(PlayerSelectionService selection, IPluginLogger logger)
    : MprisCommandBase(selection, logger)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "Mpris.CycleRepeat",
        DisplayName = "Media: Cycle Repeat Mode",
        Group = "Media",
        Icon = "\U000F0456",
        Description = "Switch between no repeat, repeat track and repeat playlist",
        ParameterTemplate = MprisParameters.PlayerTemplate,
        Parameters = MprisParameters.PlayerParameters
    };

    protected override async Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        PlayerState state = player.State;

        if (!Supports(player, state.Capabilities.HasLoopStatus, "repeat"))
        {
            return;
        }

        string next = state.LoopStatus switch
        {
            "None" => "Playlist",
            "Playlist" => "Track",
            _ => "None"
        };

        if (await player.SetLoopStatusAsync(next).ConfigureAwait(false))
        {
            MprisParameters.ShowOverlay(ctx, ctx.Host.Tr(MprisDisplayCommands.FormatLoopStatus(next)));
        }
    }
}
