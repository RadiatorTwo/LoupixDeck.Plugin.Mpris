namespace LoupixDeck.Plugin.Mpris;

/// <summary>Bus names, object path and interfaces defined by the MPRIS specification.</summary>
internal static class MprisServices
{
    /// <summary>Every MPRIS player owns a bus name starting with this prefix.</summary>
    public const string ServicePrefix = "org.mpris.MediaPlayer2.";

    public const string ObjectPath = "/org/mpris/MediaPlayer2";
    public const string RootInterface = "org.mpris.MediaPlayer2";
    public const string PlayerInterface = "org.mpris.MediaPlayer2.Player";

    /// <summary>True when the bus name belongs to an MPRIS player.</summary>
    public static bool IsPlayerService(string busName)
    {
        return busName.StartsWith(ServicePrefix, StringComparison.Ordinal) && busName.Length > ServicePrefix.Length;
    }

    /// <summary>
    /// The part of the bus name that identifies the application. Browsers append a changing
    /// instance suffix (org.mpris.MediaPlayer2.firefox.instance_1_23), so a stable comparison
    /// has to use this value rather than the full bus name.
    /// </summary>
    public static string GetApplicationName(string busName)
    {
        if (!IsPlayerService(busName))
        {
            return busName;
        }

        string suffix = busName[ServicePrefix.Length..];
        int dot = suffix.IndexOf('.');
        return dot < 0 ? suffix : suffix[..dot];
    }
}
