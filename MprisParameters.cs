using System.Globalization;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// The per-assignment parameters of the commands. The host hands them over as plain strings, so
/// every value is parsed defensively and an absent one falls back to the documented default.
/// </summary>
internal static class MprisParameters
{
    public const string StrategyName = "strategy";
    public const string PlayerName = "player";
    public const string SecondsName = "seconds";
    public const string StepName = "step";
    public const string PercentName = "percent";
    public const string PositionName = "position";

    /// <summary>Strategy and target player, the two values every command needs.</summary>
    public const string PlayerTemplate = "({strategy},{player})";

    public const string SecondsTemplate = "({strategy},{player},{seconds})";
    public const string StepTemplate = "({strategy},{player},{step})";
    public const string PercentTemplate = "({strategy},{player},{percent})";
    public const string PositionTemplate = "({strategy},{player},{position})";

    public static IReadOnlyList<CommandParameter> PlayerParameters { get; } =
    [
        new CommandParameter(StrategyName, typeof(string)) { DefaultValue = PlayerStrategyParser.AutomaticValue },
        new CommandParameter(PlayerName, typeof(string))
    ];

    public static IReadOnlyList<CommandParameter> SeekParameters { get; } =
    [
        new CommandParameter(StrategyName, typeof(string)) { DefaultValue = PlayerStrategyParser.AutomaticValue },
        new CommandParameter(PlayerName, typeof(string)),
        new CommandParameter(SecondsName, typeof(int))
        {
            DefaultValue = MprisSettings.DefaultSeekStepSeconds.ToString(CultureInfo.InvariantCulture)
        }
    ];

    public static IReadOnlyList<CommandParameter> VolumeStepParameters { get; } =
    [
        new CommandParameter(StrategyName, typeof(string)) { DefaultValue = PlayerStrategyParser.AutomaticValue },
        new CommandParameter(PlayerName, typeof(string)),
        new CommandParameter(StepName, typeof(int))
        {
            DefaultValue = MprisSettings.DefaultVolumeStepPercent.ToString(CultureInfo.InvariantCulture)
        }
    ];

    public static IReadOnlyList<CommandParameter> VolumePercentParameters { get; } =
    [
        new CommandParameter(StrategyName, typeof(string)) { DefaultValue = PlayerStrategyParser.AutomaticValue },
        new CommandParameter(PlayerName, typeof(string)),
        new CommandParameter(PercentName, typeof(int)) { DefaultValue = "50" }
    ];

    public static IReadOnlyList<CommandParameter> PositionParameters { get; } =
    [
        new CommandParameter(StrategyName, typeof(string)) { DefaultValue = PlayerStrategyParser.AutomaticValue },
        new CommandParameter(PlayerName, typeof(string)),
        new CommandParameter(PositionName, typeof(int)) { DefaultValue = "0" }
    ];

    public static PlayerStrategy ReadStrategy(CommandContext ctx) => PlayerStrategyParser.Parse(Read(ctx, 0));

    public static string? ReadPlayer(CommandContext ctx) => Read(ctx, 1);

    /// <summary>
    /// Reads a step parameter. A missing, unreadable or zero value means the button carries no
    /// step of its own - a binding saved with an empty parameter stores a plain 0 - so the step
    /// from the settings is used instead of seeking or changing the volume by nothing.
    /// </summary>
    public static int ReadStep(CommandContext ctx, int index, int fallback)
    {
        int value = ReadNumber(ctx, index, fallback);
        return value == 0 ? fallback : Math.Abs(value);
    }

    /// <summary>Reads an integer parameter, falling back to the given default.</summary>
    public static int ReadNumber(CommandContext ctx, int index, int fallback)
    {
        string? value = Read(ctx, index);
        if (value is not null &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }

        return fallback;
    }

    /// <summary>Builds the parameter map an IMenuContributor node hands to a command.</summary>
    public static Dictionary<string, string> Build(PlayerStrategy strategy, string? player = null)
    {
        Dictionary<string, string> parameters = new(StringComparer.Ordinal)
        {
            [StrategyName] = PlayerStrategyParser.ToParameterValue(strategy),
            [PlayerName] = player ?? string.Empty
        };

        return parameters;
    }

    /// <summary>Shows a short confirmation on the touch slot of the rotary that triggered the command.</summary>
    public static void ShowOverlay(CommandContext ctx, string text)
    {
        if (ctx.SourceIndex is not int rotaryIndex)
        {
            return;
        }

        int slot = ctx.Host.GetTouchSlotForRotary(rotaryIndex);
        if (slot < 0)
        {
            return;
        }

        ctx.Host.OverlayTouchText(slot, text, TimeSpan.FromSeconds(1.5));
    }

    private static string? Read(CommandContext ctx, int index)
    {
        string[] parameters = ctx.Parameters;
        if (parameters.Length <= index)
        {
            return null;
        }

        string value = parameters[index];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
