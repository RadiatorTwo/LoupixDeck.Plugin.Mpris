using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>Opens the folder that lists the running players.</summary>
internal sealed class MprisSelectPlayerCommand(Func<PlayerFolderProvider> folderFactory, IPluginLogger logger) : IPluginCommand
{
    public CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "Mpris.SelectPlayer",
        DisplayName = "Media: Select Player",
        Group = "Media",
        Icon = "\U000F0387",
        Description = "Open the list of running media players and pick the preferred one"
    };

    public ButtonTargets SupportedTargets => ButtonTargets.TouchButton;

    public Task Execute(CommandContext ctx)
    {
        try
        {
            ctx.Host.OpenFolder(folderFactory());
        }
        catch (Exception ex)
        {
            logger.Error($"MPRIS: {Descriptor.CommandName} failed.", ex);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Stores the player a command resolves to as the preferred player.</summary>
internal sealed class MprisSetPreferredPlayerCommand(
    PlayerSelectionService selection,
    MprisSettings settings,
    IPluginLogger logger)
    : MprisCommandBase(selection, logger)
{
    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "Mpris.SetPreferredPlayer",
        DisplayName = "Media: Set Preferred Player",
        Group = "Media",
        Icon = "\U000F0389",
        Description = "Make the selected player the preferred one",
        ParameterTemplate = MprisParameters.PlayerTemplate,
        Parameters = MprisParameters.PlayerParameters
    };

    protected override Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        settings.PreferredPlayer = player.ServiceName;
        MprisParameters.ShowOverlay(ctx, player.State.DisplayName);
        Logger.Info($"MPRIS: {player.ServiceName} is now the preferred player.");
        return Task.CompletedTask;
    }
}
