using Tmds.DBus.Protocol;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// The subset of the MPRIS metadata map this plugin shows. Every field is optional: players
/// leave entries out and sometimes send them with an unexpected type, which must never throw.
/// </summary>
internal sealed record MediaMetadata(
    string? TrackId,
    string? Title,
    string? Artist,
    string? Album,
    string? ArtUrl,
    TimeSpan? Length)
{
    public static readonly MediaMetadata Empty = new(null, null, null, null, null, null);

    public bool HasTrack => !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Artist);

    /// <summary>Reads a metadata map defensively. Unknown or wrongly typed entries become null.</summary>
    public static MediaMetadata Parse(VariantValue value)
    {
        if (value.Type == VariantValueType.Variant)
        {
            value = value.GetVariantValue();
        }

        if (value.Type != VariantValueType.Dictionary)
        {
            return Empty;
        }

        string? trackId = null;
        string? title = null;
        string? artist = null;
        string? album = null;
        string? artUrl = null;
        TimeSpan? length = null;

        for (int i = 0; i < value.Count; i++)
        {
            KeyValuePair<VariantValue, VariantValue> entry = value.GetDictionaryEntry(i);
            if (entry.Key.Type != VariantValueType.String)
            {
                continue;
            }

            VariantValue item = VariantReader.Unwrap(entry.Value);

            switch (entry.Key.GetString())
            {
                case "mpris:trackid":
                    trackId = VariantReader.AsObjectPathOrString(item);
                    break;
                case "xesam:title":
                    title = VariantReader.AsString(item);
                    break;
                case "xesam:artist":
                case "xesam:albumArtist":
                    artist ??= VariantReader.AsStringList(item);
                    break;
                case "xesam:album":
                    album = VariantReader.AsString(item);
                    break;
                case "mpris:artUrl":
                    artUrl = VariantReader.AsString(item);
                    break;
                case "mpris:length":
                    long? microseconds = VariantReader.AsInt64(item);
                    length = microseconds is > 0 ? TimeSpan.FromTicks(microseconds.Value * 10) : null;
                    break;
            }
        }

        return new MediaMetadata(trackId, title, artist, album, artUrl, length);
    }
}
