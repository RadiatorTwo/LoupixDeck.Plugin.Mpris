using Tmds.DBus.Protocol;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// Defensive readers for D-Bus variants. A player that sends a value with an unexpected type
/// must not break the plugin, so every reader returns null instead of throwing.
/// </summary>
internal static class VariantReader
{
    /// <summary>Unwraps a nested variant, which is what a{sv} entries usually carry.</summary>
    public static VariantValue Unwrap(VariantValue value)
    {
        return value.Type == VariantValueType.Variant ? value.GetVariantValue() : value;
    }

    public static string? AsString(VariantValue value)
    {
        value = Unwrap(value);
        return value.Type == VariantValueType.String ? NullIfEmpty(value.GetString()) : null;
    }

    public static string? AsObjectPathOrString(VariantValue value)
    {
        value = Unwrap(value);
        return value.Type switch
        {
            VariantValueType.ObjectPath => NullIfEmpty(value.GetObjectPathAsString()),
            VariantValueType.String => NullIfEmpty(value.GetString()),
            _ => null
        };
    }

    /// <summary>Joins an array of strings, the shape xesam:artist uses. A plain string is accepted too.</summary>
    public static string? AsStringList(VariantValue value)
    {
        value = Unwrap(value);

        if (value.Type == VariantValueType.String)
        {
            return NullIfEmpty(value.GetString());
        }

        if (value.Type != VariantValueType.Array)
        {
            return null;
        }

        List<string> parts = [];
        for (int i = 0; i < value.Count; i++)
        {
            string? part = AsString(value.GetItem(i));
            if (part is not null)
            {
                parts.Add(part);
            }
        }

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    public static bool? AsBool(VariantValue value)
    {
        value = Unwrap(value);
        return value.Type == VariantValueType.Bool ? value.GetBool() : null;
    }

    public static double? AsDouble(VariantValue value)
    {
        value = Unwrap(value);
        return value.Type switch
        {
            VariantValueType.Double => value.GetDouble(),
            VariantValueType.Int32 => value.GetInt32(),
            VariantValueType.Int64 => value.GetInt64(),
            VariantValueType.UInt32 => value.GetUInt32(),
            VariantValueType.UInt64 => value.GetUInt64(),
            _ => null
        };
    }

    public static long? AsInt64(VariantValue value)
    {
        value = Unwrap(value);
        return value.Type switch
        {
            VariantValueType.Int64 => value.GetInt64(),
            VariantValueType.Int32 => value.GetInt32(),
            VariantValueType.UInt32 => value.GetUInt32(),
            VariantValueType.UInt64 => (long)value.GetUInt64(),
            VariantValueType.Double => (long)value.GetDouble(),
            _ => null
        };
    }

    /// <summary>Microseconds, the unit MPRIS uses for Position, Length and Seek.</summary>
    public static TimeSpan? AsDuration(VariantValue value)
    {
        long? microseconds = AsInt64(value);
        return microseconds is null ? null : TimeSpan.FromTicks(microseconds.Value * 10);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
