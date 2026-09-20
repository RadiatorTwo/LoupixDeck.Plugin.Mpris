using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// Controls every MPRIS-compatible media player through the D-Bus session bus.
/// </summary>
public sealed class MprisPlugin : LoupixPlugin
{
    private readonly List<IPluginCommand> _commands = [];

    private IPluginHost? _host;
    private MprisConnection? _connection;
    private PlayerRegistry? _registry;

    public override PluginMetadata Metadata { get; } = new()
    {
        Id = "mpris",
        Name = "MPRIS Media",
        Version = new Version(1, 0, 0),
        SdkVersion = new Version(1, 24, 0),
        Author = "RadiatorTwo",
        Description = "Controls every MPRIS-compatible media player over the D-Bus session bus."
    };

    public override void Initialize(IPluginHost host)
    {
        _host = host;

        try
        {
            _connection = new MprisConnection(host.Logger);
            _registry = new PlayerRegistry(_connection, host.Logger);

            // Connecting talks to the bus, so it must not block the host's Initialize call.
            _ = ConnectAsync(_connection, _registry);
        }
        catch (Exception ex)
        {
            host.Logger.Error("MPRIS: the plugin could not be initialized.", ex);
            Shutdown();
        }
    }

    public override IEnumerable<IPluginCommand> GetCommands() => _commands;

    public override void Shutdown()
    {
        _commands.Clear();
        _registry?.Dispose();
        _registry = null;
        _connection?.Dispose();
        _connection = null;
        base.Shutdown();
    }

    private async Task ConnectAsync(MprisConnection connection, PlayerRegistry registry)
    {
        try
        {
            if (await connection.ConnectAsync().ConfigureAwait(false))
            {
                await registry.StartAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _host?.Logger.Error("MPRIS: the session bus could not be opened.", ex);
        }
    }
}
