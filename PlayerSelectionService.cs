using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// Turns a strategy into the player a command acts on. This is the only place that decides,
/// so the result is the same for every command and reproducible for the same bus state.
/// </summary>
internal sealed class PlayerSelectionService(PlayerRegistry registry, MprisSettings settings, IPluginLogger logger)
{
    /// <summary>Application names of the browsers whose players the user can hide.</summary>
    private static readonly string[] BrowserApplications =
    [
        "firefox",
        "librewolf",
        "waterfox",
        "zen",
        "chrome",
        "chromium",
        "brave",
        "vivaldi",
        "opera",
        "msedge",
        "epiphany"
    ];

    /// <summary>The players a listing shows, with the browser players filtered out when asked.</summary>
    public IReadOnlyList<PlayerState> VisiblePlayers
    {
        get
        {
            IReadOnlyList<PlayerState> players = registry.Players;
            if (settings.ShowBrowserPlayers)
            {
                return players;
            }

            return [.. players.Where(player => !IsBrowser(player))];
        }
    }

    /// <summary>Resolves the player for a strategy, or null when no player matches.</summary>
    public PlayerState? Resolve(PlayerStrategy strategy, string? fixedPlayer)
    {
        return strategy switch
        {
            PlayerStrategy.CurrentlyPlaying => ResolveCurrentlyPlaying(),
            PlayerStrategy.Preferred => ResolveNamed(settings.PreferredPlayer, "preferred"),
            PlayerStrategy.Fixed => ResolveNamed(fixedPlayer, "fixed"),
            _ => ResolveAutomatic()
        };
    }

    /// <summary>Resolves the proxy for a strategy. Null means the command is a no-op.</summary>
    public PlayerProxy? ResolveProxy(PlayerStrategy strategy, string? fixedPlayer)
    {
        PlayerState? state = Resolve(strategy, fixedPlayer);
        return state is null ? null : registry.GetProxy(state.ServiceName);
    }

    /// <summary>Finds a player by bus name, falling back to the application name a browser keeps stable.</summary>
    public PlayerState? Find(string? nameOrService)
    {
        if (string.IsNullOrWhiteSpace(nameOrService))
        {
            return null;
        }

        string wanted = nameOrService.Trim();
        IReadOnlyList<PlayerState> players = registry.Players;

        PlayerState? exact = players.FirstOrDefault(
            player => string.Equals(player.ServiceName, wanted, StringComparison.Ordinal));

        if (exact is not null)
        {
            return exact;
        }

        // Browsers append a changing instance suffix, so a stored binding has to match on the
        // application part of the bus name as well.
        return MostRelevant(players.Where(
            player => string.Equals(player.ApplicationName, MprisServices.GetApplicationName(wanted), StringComparison.OrdinalIgnoreCase)));
    }

    public static bool IsBrowser(PlayerState player)
    {
        string application = player.ApplicationName;
        return BrowserApplications.Any(browser => application.Contains(browser, StringComparison.OrdinalIgnoreCase));
    }

    private PlayerState? ResolveAutomatic()
    {
        if (!settings.AutomaticSelection)
        {
            return null;
        }

        return MostRelevant(VisiblePlayers);
    }

    private PlayerState? ResolveCurrentlyPlaying()
    {
        return MostRelevant(VisiblePlayers.Where(player => player.Status == PlaybackStatus.Playing));
    }

    private PlayerState? ResolveNamed(string? nameOrService, string kind)
    {
        PlayerState? player = Find(nameOrService);
        if (player is not null)
        {
            return player;
        }

        if (string.IsNullOrWhiteSpace(nameOrService))
        {
            logger.Warn($"MPRIS: no {kind} player is configured, the command does nothing.");
        }
        else
        {
            logger.Warn($"MPRIS: the {kind} player '{nameOrService}' is not running.");
        }

        return settings.FallbackWhenUnavailable ? MostRelevant(VisiblePlayers) : null;
    }

    /// <summary>
    /// Orders players so the result is deterministic: playing before paused before stopped, and
    /// among those the most recently changed one, with the bus name as the final tie breaker.
    /// </summary>
    private static PlayerState? MostRelevant(IEnumerable<PlayerState> players)
    {
        return players
            .OrderByDescending(player => StatusRank(player.Status))
            .ThenByDescending(player => player.LastActivity)
            .ThenBy(player => player.ServiceName, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static int StatusRank(PlaybackStatus status) => status switch
    {
        PlaybackStatus.Playing => 3,
        PlaybackStatus.Paused => 2,
        PlaybackStatus.Stopped => 1,
        _ => 0
    };
}
