using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.MPRISMedia;

public sealed class MPRISMediaPlugin : LoupixPlugin
{
    private IPluginHost? _host;

    public override PluginMetadata Metadata { get; } = new()
    {
        Id = "mprismedia",
        Name = "MPRISMedia",
        Version = new Version(1, 0, 0),
        SdkVersion = new Version(1, 24, 0),
        Author = "",
        Description = ""
    };

    public override void Initialize(IPluginHost host)
    {
        _host = host;
    }

    public override IEnumerable<IPluginCommand> GetCommands() => [];

    /// <summary>
    /// Translates English text the plugin builds while running. Descriptor text is
    /// translated by the host through the same strings.&lt;code&gt;.json files.
    /// </summary>
    private string Tr(string english) => _host?.Tr(english) ?? english;
}
