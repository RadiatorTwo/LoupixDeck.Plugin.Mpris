using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// Draws the cover, the track and a progress bar on a touch button, and toggles playback when
/// pressed. Rendering only reads cached state, the artwork is loaded in the background.
/// </summary>
internal sealed class MprisArtworkCommand(
    PlayerSelectionService selection,
    PositionTracker positions,
    ArtworkCache artwork,
    IPluginLogger logger)
    : MprisCommandBase(selection, logger), IDisplayImageCommand
{
    private static readonly PluginColor Background = PluginColor.FromRgb(16, 16, 16);
    private static readonly PluginColor Text = PluginColor.White;
    private static readonly PluginColor Muted = PluginColor.FromRgb(170, 170, 170);
    private static readonly PluginColor BarBackground = PluginColor.FromRgb(60, 60, 60);
    private static readonly PluginColor BarForeground = PluginColor.FromRgb(90, 180, 255);

    public override CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = "Mpris.Artwork",
        DisplayName = "Media: Artwork",
        Group = "Media",
        Icon = "\U000F075A",
        Description = "Show the cover, the track and the progress of the selected player",
        ParameterTemplate = MprisParameters.PlayerTemplate,
        Parameters = MprisParameters.PlayerParameters
    };

    public override ButtonTargets SupportedTargets => ButtonTargets.TouchButton;

    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas)
    {
        try
        {
            PlayerState? state = Selection.Resolve(MprisParameters.ReadStrategy(ctx), MprisParameters.ReadPlayer(ctx));

            canvas.Clear(Background);

            if (state is null)
            {
                canvas.DrawText(ctx.Host.Tr("No player"), 0, 0, canvas.Width, canvas.Height, Muted, 12f);
                return true;
            }

            byte[]? cover = artwork.Get(state.Metadata.ArtUrl);

            if (cover is not null)
            {
                canvas.DrawImage(cover, 0, 0, canvas.Width, canvas.Height);
                // The cover keeps its own colors, so the text needs a darker band behind it.
                canvas.FillRectangle(0, canvas.Height - 34, canvas.Width, 34, PluginColor.FromRgb(0, 0, 0) with { A = 190 });
            }
            else
            {
                canvas.DrawSymbol("music", canvas.Width / 4, 6, canvas.Width / 2, canvas.Width / 2, Muted);
            }

            DrawTrack(canvas, state);
            DrawProgress(canvas, state);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"MPRIS: {Descriptor.CommandName} could not be rendered.", ex);
            return false;
        }
    }

    protected override Task ExecuteOn(PlayerProxy player, CommandContext ctx)
    {
        PlayerState state = player.State;
        bool capability = state.Status == PlaybackStatus.Playing
            ? state.Capabilities.CanPause
            : state.Capabilities.CanPlay;

        return Supports(player, capability, "play/pause") ? player.PlayPauseAsync() : Task.CompletedTask;
    }

    private static void DrawTrack(IRenderCanvas canvas, PlayerState state)
    {
        string title = state.Metadata.Title ?? state.DisplayName;
        string subtitle = $"{MprisDisplayCommands.StatusGlyph(state.Status)} {state.Metadata.Artist ?? state.DisplayName}";

        canvas.DrawText(title, 2, canvas.Height - 34, canvas.Width - 4, 16, Text, 12f, TextHAlign.Center, TextVAlign.Middle, bold: true);
        canvas.DrawText(subtitle, 2, canvas.Height - 20, canvas.Width - 4, 14, Muted, 10f, TextHAlign.Center, TextVAlign.Middle);
    }

    private void DrawProgress(IRenderCanvas canvas, PlayerState state)
    {
        double? progress = positions.GetProgress(state);
        if (progress is null)
        {
            return;
        }

        int width = canvas.Width - 8;
        int y = canvas.Height - 5;

        canvas.FillRectangle(4, y, width, 3, BarBackground);
        canvas.FillRectangle(4, y, (int)Math.Round(width * progress.Value), 3, BarForeground);
    }
}
