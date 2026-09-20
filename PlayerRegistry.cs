using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// Knows every MPRIS player that currently owns a bus name and holds its cached state. Players
/// appear and disappear through NameOwnerChanged, so no polling is needed.
/// </summary>
internal sealed class PlayerRegistry : IDisposable
{
    private readonly MprisConnection _connection;
    private readonly IPluginLogger _logger;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, PlayerProxy> _players = new(StringComparer.Ordinal);

    private IReadOnlyList<PlayerState> _snapshot = [];
    private bool _disposed;

    public PlayerRegistry(MprisConnection connection, IPluginLogger logger)
    {
        _connection = connection;
        _logger = logger;
        _connection.PlayerServiceChanged += OnPlayerServiceChanged;
        _connection.ConnectionReady += OnConnectionReady;
    }

    /// <summary>The known players, ordered by name so the folder and menus stay stable.</summary>
    public IReadOnlyList<PlayerState> Players => _snapshot;

    /// <summary>Raised when a player appeared or disappeared.</summary>
    public event Action? PlayersChanged;

    /// <summary>Raised when any player's state changed, including its own appearance.</summary>
    public event Action<PlayerState>? PlayerUpdated;

    /// <summary>Reads the players that are already running. Called once after connecting.</summary>
    public async Task StartAsync()
    {
        IReadOnlyList<string> services = await _connection.ListPlayerServicesAsync().ConfigureAwait(false);

        foreach (string service in services)
        {
            await AddPlayerAsync(service).ConfigureAwait(false);
        }
    }

    public PlayerProxy? GetProxy(string serviceName)
    {
        lock (_gate)
        {
            return _players.GetValueOrDefault(serviceName);
        }
    }

    public PlayerState? GetState(string serviceName) => GetProxy(serviceName)?.State;

    public void Dispose()
    {
        _disposed = true;
        _connection.PlayerServiceChanged -= OnPlayerServiceChanged;
        _connection.ConnectionReady -= OnConnectionReady;

        PlayerProxy[] players;
        lock (_gate)
        {
            players = [.. _players.Values];
            _players.Clear();
            _snapshot = [];
        }

        foreach (PlayerProxy player in players)
        {
            player.Dispose();
        }

        PlayersChanged = null;
        PlayerUpdated = null;
    }

    private void OnPlayerServiceChanged(string serviceName, bool hasOwner)
    {
        if (hasOwner)
        {
            _ = AddPlayerAsync(serviceName);
        }
        else
        {
            RemovePlayer(serviceName);
        }
    }

    private void OnConnectionReady()
    {
        // The old proxies belonged to the previous connection and their watches are dead.
        PlayerProxy[] stale;
        lock (_gate)
        {
            stale = [.. _players.Values];
            _players.Clear();
        }

        foreach (PlayerProxy player in stale)
        {
            player.Dispose();
        }

        PublishSnapshot();
        _ = StartAsync();
    }

    private async Task AddPlayerAsync(string serviceName)
    {
        DBusClient? client = _connection.Client;
        if (client is null || _disposed)
        {
            return;
        }

        PlayerProxy proxy = new(client, serviceName, _logger);

        lock (_gate)
        {
            if (_disposed || _players.ContainsKey(serviceName))
            {
                proxy.Dispose();
                return;
            }

            _players[serviceName] = proxy;
        }

        proxy.Changed += OnPlayerChanged;
        PublishSnapshot();

        try
        {
            await proxy.InitializeAsync().ConfigureAwait(false);
            _logger.Info($"MPRIS: player {serviceName} appeared.");
        }
        catch (Exception ex)
        {
            // A player that cannot be read stays listed but keeps its empty state, so one broken
            // player cannot take the plugin down.
            _logger.Warn($"MPRIS: cannot read player {serviceName}: {ex.Message}");
        }

        PublishSnapshot();
        RaisePlayerUpdated(proxy.State);
    }

    private void RemovePlayer(string serviceName)
    {
        PlayerProxy? proxy;

        lock (_gate)
        {
            if (!_players.Remove(serviceName, out proxy))
            {
                return;
            }
        }

        proxy.Changed -= OnPlayerChanged;
        proxy.Dispose();
        _logger.Info($"MPRIS: player {serviceName} disappeared.");
        PublishSnapshot();
    }

    private void OnPlayerChanged(PlayerState state)
    {
        PublishSnapshot();
        RaisePlayerUpdated(state);
    }

    private void PublishSnapshot()
    {
        PlayerState[] states;

        lock (_gate)
        {
            states = [.. _players.Values.Select(player => player.State).OrderBy(state => state.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
        }

        _snapshot = states;

        try
        {
            PlayersChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.Error("MPRIS: a player list handler failed.", ex);
        }
    }

    private void RaisePlayerUpdated(PlayerState state)
    {
        try
        {
            PlayerUpdated?.Invoke(state);
        }
        catch (Exception ex)
        {
            _logger.Error("MPRIS: a player state handler failed.", ex);
        }
    }
}
