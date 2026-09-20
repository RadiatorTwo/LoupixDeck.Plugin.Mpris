using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// Controls every MPRIS-compatible media player through the D-Bus session bus: transport,
/// seeking, volume, dynamic displays and a folder that lists the running players.
/// </summary>
public sealed class MprisPlugin : LoupixPlugin, IMenuContributor, IPluginSettingsPage
{
    /// <summary>A player can push several property changes at once, so redraws are coalesced.</summary>
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(200);

    private readonly List<IPluginCommand> _commands = [];
    private readonly List<string> _displayCommandNames = [];
    private readonly Lock _refreshGate = new();

    private Timer? _refreshTimer;

    private IPluginHost? _host;
    private MprisConnection? _connection;
    private PlayerRegistry? _registry;
    private PlayerSelectionService? _selection;
    private PositionTracker? _positions;
    private ArtworkCache? _artwork;
    private MprisSettings? _settings;
    private MprisMenu? _menu;
    private MprisSettingsPage? _settingsPage;

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
            MprisSettings settings = new(host.Settings);
            MprisConnection connection = new(host.Logger);
            PlayerRegistry registry = new(connection, host.Logger);
            PlayerSelectionService selection = new(registry, settings, host.Logger);
            PositionTracker positions = new(registry, host.Logger);
            ArtworkCache artwork = new(settings, host.Logger);

            _settings = settings;
            _connection = connection;
            _registry = registry;
            _selection = selection;
            _positions = positions;
            _artwork = artwork;
            _menu = new MprisMenu(selection);
            _settingsPage = new MprisSettingsPage(registry, selection, settings, host);

            BuildCommands(host, settings, selection, positions, artwork, registry);

            _refreshTimer = new Timer(_ => PushRefresh(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            registry.PlayerUpdated += OnPlayerUpdated;
            registry.PlayersChanged += RefreshDisplays;
            artwork.ArtworkLoaded += RefreshDisplays;

            // Connecting talks to the bus, so it must not block the host's Initialize call.
            _ = ConnectAsync(connection, registry);
        }
        catch (Exception ex)
        {
            host.Logger.Error("MPRIS: the plugin could not be initialized.", ex);
            Shutdown();
        }
    }

    public override IEnumerable<IPluginCommand> GetCommands() => _commands;

    public override IReadOnlyList<CommandGroupDescriptor> GetCommandGroups() =>
    [
        new CommandGroupDescriptor
        {
            Group = "Media",
            Icon = "\U000F075A",
            Description = "Control MPRIS media players",
            Section = CommandGroupSection.Plugins
        }
    ];

    /// <summary>Ready-made rotary configurations offered in the encoder's context menu.</summary>
    public override IEnumerable<DialPresetDescriptor> GetDialPresets()
    {
        Dictionary<string, string> Parameters() => MprisParameters.Build(PlayerStrategy.Automatic);

        yield return new DialPresetDescriptor
        {
            Id = "media-volume",
            Name = "Media Volume",
            Glyph = "\U000F057E",
            Actions = new Dictionary<RotaryAction, MenuCommandRef>
            {
                [RotaryAction.CounterClockwise] = new() { CommandName = "Mpris.VolumeDown", Parameters = Parameters() },
                [RotaryAction.Clockwise] = new() { CommandName = "Mpris.VolumeUp", Parameters = Parameters() },
                [RotaryAction.Press] = new() { CommandName = "Mpris.PlayPause", Parameters = Parameters() }
            }
        };

        yield return new DialPresetDescriptor
        {
            Id = "media-seek",
            Name = "Media Seek",
            Glyph = "\U000F0954",
            Actions = new Dictionary<RotaryAction, MenuCommandRef>
            {
                [RotaryAction.CounterClockwise] = new() { CommandName = "Mpris.SeekBackward", Parameters = Parameters() },
                [RotaryAction.Clockwise] = new() { CommandName = "Mpris.SeekForward", Parameters = Parameters() },
                [RotaryAction.Press] = new() { CommandName = "Mpris.PlayPause", Parameters = Parameters() }
            }
        };
    }

    public Task<IReadOnlyList<MenuNode>> GetMenuNodes(ButtonTargets target)
    {
        MprisMenu? menu = _menu;
        return Task.FromResult(menu is null ? [] : menu.Build(target));
    }

    public IReadOnlyList<PluginSettingDescriptor> SettingsSchema =>
        _settingsPage?.BuildSchema() ?? [];

    public IReadOnlyList<PluginSettingAction> SettingsActions =>
        _settingsPage?.BuildActions() ?? [];

    public void OnSettingsSaved()
    {
        // Every setting is read on demand. Only the artwork cache holds derived data, and it has
        // to go so a newly allowed download is attempted again.
        _artwork?.Clear();
        RefreshDisplays();
    }

    public override void Shutdown()
    {
        if (_registry is not null)
        {
            _registry.PlayerUpdated -= OnPlayerUpdated;
            _registry.PlayersChanged -= RefreshDisplays;
        }

        if (_artwork is not null)
        {
            _artwork.ArtworkLoaded -= RefreshDisplays;
        }

        _refreshTimer?.Dispose();
        _refreshTimer = null;
        _commands.Clear();
        _displayCommandNames.Clear();
        _positions?.Dispose();
        _positions = null;
        _artwork?.Dispose();
        _artwork = null;
        _registry?.Dispose();
        _registry = null;
        _connection?.Dispose();
        _connection = null;
        _selection = null;
        _settings = null;
        _menu = null;
        _settingsPage = null;
        base.Shutdown();
    }

    private void BuildCommands(
        IPluginHost host,
        MprisSettings settings,
        PlayerSelectionService selection,
        PositionTracker positions,
        ArtworkCache artwork,
        PlayerRegistry registry)
    {
        IPluginLogger logger = host.Logger;

        _commands.AddRange(
        [
            new MprisPlayPauseCommand(selection, logger),
            new MprisPlayCommand(selection, logger),
            new MprisPauseCommand(selection, logger),
            new MprisStopCommand(selection, logger),
            new MprisNextCommand(selection, logger),
            new MprisPreviousCommand(selection, logger),
            new MprisSeekCommand(selection, settings, positions, logger, forward: true),
            new MprisSeekCommand(selection, settings, positions, logger, forward: false),
            new MprisSeekToPositionCommand(selection, positions, logger),
            new MprisVolumeStepCommand(selection, settings, logger, up: true),
            new MprisVolumeStepCommand(selection, settings, logger, up: false),
            new MprisSetVolumeCommand(selection, logger),
            new MprisToggleShuffleCommand(selection, logger),
            new MprisCycleRepeatCommand(selection, logger),
            new MprisSetPreferredPlayerCommand(selection, settings, logger),
            new MprisSelectPlayerCommand(
                () => new PlayerFolderProvider(registry, selection, settings, positions, host),
                logger),
            new MprisArtworkCommand(selection, positions, artwork, logger)
        ]);

        _commands.AddRange(MprisDisplayCommands.Create(selection, positions, logger));

        _displayCommandNames.AddRange(_commands
            .Where(command => command is IDisplayCommand or IDisplayImageCommand)
            .Select(command => command.Descriptor.CommandName));
    }

    private void OnPlayerUpdated(PlayerState state) => RefreshDisplays();

    /// <summary>
    /// Asks for a redraw of the display commands. A player often sends several properties in a
    /// row, so the requests are coalesced into one push shortly afterwards.
    /// </summary>
    private void RefreshDisplays()
    {
        lock (_refreshGate)
        {
            _refreshTimer?.Change(RefreshDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void PushRefresh()
    {
        IPluginHost? host = _host;
        if (host is null)
        {
            return;
        }

        foreach (string commandName in _displayCommandNames)
        {
            try
            {
                host.RequestButtonRefresh(commandName);
            }
            catch (Exception ex)
            {
                host.Logger.Warn($"MPRIS: cannot refresh {commandName}: {ex.Message}");
            }
        }
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
