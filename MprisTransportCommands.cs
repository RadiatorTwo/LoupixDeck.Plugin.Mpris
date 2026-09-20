using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>Toggles between playing and paused.</summary>
internal sealed class MprisPlayPauseCommand(PlayerSelectionService selection, IPluginLogger logger)
    : MprisCommandBase(selection, logger)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "Mpris.PlayPause",
        DisplayName = "Media: Play/Pause",
        Group = "Media",
        Icon = "\U000F040C",
        Description = "Toggle playback of the selected media player",
        ParameterTemplate = MprisParameters.PlayerTemplate,
        Parameters = MprisParameters.PlayerParameters
    };

    protected override Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        PlayerState state = player.State;
        bool capability = state.Status == PlaybackStatus.Playing
            ? state.Capabilities.CanPause
            : state.Capabilities.CanPlay;

        return Supports(player, capability, "play/pause") ? player.PlayPauseAsync() : Task.CompletedTask;
    }
}

/// <summary>Starts playback.</summary>
internal sealed class MprisPlayCommand(PlayerSelectionService selection, IPluginLogger logger)
    : MprisCommandBase(selection, logger)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "Mpris.Play",
        DisplayName = "Media: Play",
        Group = "Media",
        Icon = "\U000F040A",
        Description = "Start playback of the selected media player",
        ParameterTemplate = MprisParameters.PlayerTemplate,
        Parameters = MprisParameters.PlayerParameters
    };

    protected override Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        return Supports(player, player.State.Capabilities.CanPlay, "play") ? player.PlayAsync() : Task.CompletedTask;
    }
}

/// <summary>Pauses playback.</summary>
internal sealed class MprisPauseCommand(PlayerSelectionService selection, IPluginLogger logger)
    : MprisCommandBase(selection, logger)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "Mpris.Pause",
        DisplayName = "Media: Pause",
        Group = "Media",
        Icon = "\U000F03E4",
        Description = "Pause the selected media player",
        ParameterTemplate = MprisParameters.PlayerTemplate,
        Parameters = MprisParameters.PlayerParameters
    };

    protected override Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        return Supports(player, player.State.Capabilities.CanPause, "pause") ? player.PauseAsync() : Task.CompletedTask;
    }
}

/// <summary>Stops playback.</summary>
internal sealed class MprisStopCommand(PlayerSelectionService selection, IPluginLogger logger)
    : MprisCommandBase(selection, logger)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "Mpris.Stop",
        DisplayName = "Media: Stop",
        Group = "Media",
        Icon = "\U000F04DB",
        Description = "Stop the selected media player",
        ParameterTemplate = MprisParameters.PlayerTemplate,
        Parameters = MprisParameters.PlayerParameters
    };

    protected override Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        return Supports(player, player.State.Capabilities.CanControl, "stop") ? player.StopAsync() : Task.CompletedTask;
    }
}

/// <summary>Skips to the next track.</summary>
internal sealed class MprisNextCommand(PlayerSelectionService selection, IPluginLogger logger)
    : MprisCommandBase(selection, logger)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "Mpris.Next",
        DisplayName = "Media: Next Track",
        Group = "Media",
        Icon = "\U000F04AD",
        Description = "Skip to the next track",
        ParameterTemplate = MprisParameters.PlayerTemplate,
        Parameters = MprisParameters.PlayerParameters
    };

    protected override Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        return Supports(player, player.State.Capabilities.CanGoNext, "next track") ? player.NextAsync() : Task.CompletedTask;
    }
}

/// <summary>Skips to the previous track.</summary>
internal sealed class MprisPreviousCommand(PlayerSelectionService selection, IPluginLogger logger)
    : MprisCommandBase(selection, logger)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "Mpris.Previous",
        DisplayName = "Media: Previous Track",
        Group = "Media",
        Icon = "\U000F04AE",
        Description = "Skip to the previous track",
        ParameterTemplate = MprisParameters.PlayerTemplate,
        Parameters = MprisParameters.PlayerParameters
    };

    protected override Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        return Supports(player, player.State.Capabilities.CanGoPrevious, "previous track") ? player.PreviousAsync() : Task.CompletedTask;
    }
}
