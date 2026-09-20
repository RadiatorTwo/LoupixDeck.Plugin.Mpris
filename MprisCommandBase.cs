using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// Shared plumbing for every command: resolve the player from the strategy, skip the call when
/// the player cannot do it, and never let an exception reach the host's worker queue.
/// </summary>
internal abstract class MprisCommandBase(PlayerSelectionService selection, IPluginLogger logger) : IPluginCommand
{
    public abstract CommandDescriptor Descriptor { get; }

    public virtual ButtonTargets SupportedTargets => ButtonTargets.All;

    protected PlayerSelectionService Selection { get; } = selection;

    protected IPluginLogger Logger { get; } = logger;

    public async Task Execute(CommandContext ctx)
    {
        try
        {
            PlayerProxy? player = Selection.ResolveProxy(MprisParameters.ReadStrategy(ctx), MprisParameters.ReadPlayer(ctx));
            if (player is null)
            {
                Logger.Info($"MPRIS: {Descriptor.CommandName} found no player to act on.");
                return;
            }

            await ExecuteOn(player, ctx).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error($"MPRIS: {Descriptor.CommandName} failed.", ex);
        }
    }

    protected abstract Task ExecuteOn(PlayerProxy player, CommandContext ctx);

    /// <summary>Logs and skips an action the player says it does not support.</summary>
    protected bool Supports(PlayerProxy player, bool capability, string action)
    {
        if (capability)
        {
            return true;
        }

        Logger.Info($"MPRIS: {player.ServiceName} does not support {action}, the command does nothing.");
        return false;
    }
}
