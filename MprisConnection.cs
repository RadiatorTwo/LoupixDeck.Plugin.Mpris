using LoupixDeck.PluginSdk;
using Tmds.DBus.Protocol;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// Owns the session bus connection, reports which MPRIS players own a bus name and reconnects
/// when the bus goes away. Nothing here knows about players beyond their bus name.
/// </summary>
internal sealed class MprisConnection(IPluginLogger logger) : IDisposable
{
    private const string BusService = "org.freedesktop.DBus";
    private const string BusPath = "/org/freedesktop/DBus";
    private const string BusInterface = "org.freedesktop.DBus";

    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30)
    ];

    private readonly CancellationTokenSource _shutdown = new();

    private DBusConnection? _connection;
    private IDisposable? _nameOwnerWatch;

    /// <summary>The D-Bus helper, or null while the plugin is not connected.</summary>
    public DBusClient? Client { get; private set; }

    /// <summary>True while a session bus connection is usable.</summary>
    public bool IsConnected { get; private set; }

    /// <summary>Raised when an MPRIS player gained (true) or lost (false) its bus name.</summary>
    public event Action<string, bool>? PlayerServiceChanged;

    /// <summary>
    /// Raised once a connection is usable, including after a reconnect. Everything cached from the
    /// bus has to be rebuilt here, because the old players were bound to the old connection.
    /// </summary>
    public event Action? ConnectionReady;

    /// <summary>Connects to the session bus. Safe to call once, from Initialize.</summary>
    public async Task<bool> ConnectAsync()
    {
        string? address = DBusAddress.Session;
        if (string.IsNullOrEmpty(address))
        {
            logger.Info("MPRIS: no session bus address, the plugin stays inactive.");
            return false;
        }

        try
        {
            DBusConnection connection = new(address);
            await connection.ConnectAsync().ConfigureAwait(false);

            _connection = connection;
            Client = new DBusClient(connection, logger);
            _nameOwnerWatch = await WatchNameOwnerChangesAsync().ConfigureAwait(false);
            IsConnected = true;

            _ = Task.Run(() => MonitorConnectionAsync(connection), _shutdown.Token);
            RaiseConnectionReady();
            return true;
        }
        catch (Exception ex)
        {
            logger.Info($"MPRIS: cannot use the session bus ({ex.Message}), the plugin stays inactive.");
            Disconnect();
            return false;
        }
    }

    /// <summary>Lists the MPRIS players that currently own a bus name.</summary>
    public async Task<IReadOnlyList<string>> ListPlayerServicesAsync()
    {
        DBusConnection? connection = _connection;
        if (connection is null)
        {
            return [];
        }

        try
        {
            string[] names = await connection.ListServicesAsync().ConfigureAwait(false);
            return names.Where(MprisServices.IsPlayerService).ToArray();
        }
        catch (Exception ex)
        {
            logger.Warn($"MPRIS: cannot list the session bus services: {ex.Message}");
            return [];
        }
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _nameOwnerWatch?.Dispose();
        _nameOwnerWatch = null;
        Disconnect();
        _shutdown.Dispose();
    }

    private void RaiseConnectionReady()
    {
        try
        {
            ConnectionReady?.Invoke();
        }
        catch (Exception ex)
        {
            logger.Warn($"MPRIS: a connection handler failed: {ex.Message}");
        }
    }

    private void Disconnect()
    {
        IsConnected = false;
        Client = null;
        _connection?.Dispose();
        _connection = null;
    }

    private Task<IDisposable?> WatchNameOwnerChangesAsync()
    {
        DBusClient client = Client!;
        return client.WatchSignalAsync(
            BusService,
            BusPath,
            BusInterface,
            "NameOwnerChanged",
            ReadNameOwnerChanged,
            OnNameOwnerChanged);
    }

    private void OnNameOwnerChanged(NameOwnerChange change)
    {
        if (!MprisServices.IsPlayerService(change.Name))
        {
            return;
        }

        bool hasOwner = !string.IsNullOrEmpty(change.NewOwner);
        PlayerServiceChanged?.Invoke(change.Name, hasOwner);
    }

    private async Task MonitorConnectionAsync(DBusConnection connection)
    {
        int attempt = 0;

        while (!_shutdown.IsCancellationRequested)
        {
            Exception? error;
            try
            {
                error = await connection.DisconnectedAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (_shutdown.IsCancellationRequested)
            {
                return;
            }

            logger.Warn($"MPRIS: the session bus connection was lost ({error?.Message ?? "unknown reason"}), reconnecting.");
            IsConnected = false;

            TimeSpan delay = ReconnectDelays[Math.Min(attempt, ReconnectDelays.Length - 1)];
            attempt++;

            try
            {
                await Task.Delay(delay, _shutdown.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (await ReconnectAsync().ConfigureAwait(false))
            {
                return;
            }
        }
    }

    private Task<bool> ReconnectAsync()
    {
        _nameOwnerWatch?.Dispose();
        _nameOwnerWatch = null;
        _connection?.Dispose();
        _connection = null;
        Client = null;

        return ConnectAsync();
    }

    private static NameOwnerChange ReadNameOwnerChanged(Message message, object? state)
    {
        Reader reader = message.GetBodyReader();
        string name = reader.ReadString();
        string oldOwner = reader.ReadString();
        string newOwner = reader.ReadString();
        return new NameOwnerChange(name, oldOwner, newOwner);
    }
}

internal readonly record struct NameOwnerChange(string Name, string OldOwner, string NewOwner);
