using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Mpris;

/// <summary>
/// Caches the album art a player advertises through mpris:artUrl. Renders never block: a
/// missing image is fetched in the background and reported through <see cref="ArtworkLoaded"/>,
/// so the button draws a placeholder until then.
/// </summary>
internal sealed class ArtworkCache(MprisSettings settings, IPluginLogger logger) : IDisposable
{
    /// <summary>Hard limits of the cache, small because a button image is 90x90 pixels.</summary>
    private const int MaximumEntries = 24;

    private const long MaximumBytes = 8L * 1024 * 1024;
    private const long MaximumImageBytes = 4L * 1024 * 1024;

    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, byte[]> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _order = new();
    private readonly HashSet<string> _loading = new(StringComparer.Ordinal);

    private HttpClient? _http;
    private long _bytes;
    private bool _disposed;

    /// <summary>Raised once an image finished loading, so the button can be redrawn.</summary>
    public event Action? ArtworkLoaded;

    /// <summary>
    /// The cached image for an artUrl, or null while it is not available yet. Never does I/O on
    /// the calling thread, because this runs inside a render call.
    /// </summary>
    public byte[]? Get(string? artUrl)
    {
        if (string.IsNullOrWhiteSpace(artUrl) || _disposed)
        {
            return null;
        }

        string key = BuildKey(artUrl);

        lock (_gate)
        {
            if (_entries.TryGetValue(key, out byte[]? bytes))
            {
                Touch(key);

                // An empty entry marks a load that failed, so it is not retried on every frame.
                return bytes.Length == 0 ? null : bytes;
            }

            if (!_loading.Add(key))
            {
                return null;
            }
        }

        _ = LoadAsync(artUrl, key);
        return null;
    }

    public void Dispose()
    {
        _disposed = true;
        ArtworkLoaded = null;
        _http?.Dispose();
        _http = null;

        lock (_gate)
        {
            _entries.Clear();
            _order.Clear();
            _loading.Clear();
            _bytes = 0;
        }
    }

    /// <summary>
    /// The cache key. Local files carry their last write time so a player that reuses one cache
    /// file name for every track still shows the current cover.
    /// </summary>
    private static string BuildKey(string artUrl)
    {
        string? path = ToLocalPath(artUrl);
        if (path is null)
        {
            return artUrl;
        }

        try
        {
            return $"{path}|{File.GetLastWriteTimeUtc(path).Ticks}";
        }
        catch (Exception)
        {
            return path;
        }
    }

    /// <summary>Maps a file:// URL or a plain path to a local path, or null for a remote URL.</summary>
    private static string? ToLocalPath(string artUrl)
    {
        if (artUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(artUrl, UriKind.Absolute, out Uri? uri) ? uri.LocalPath : null;
        }

        return Path.IsPathRooted(artUrl) ? artUrl : null;
    }

    private async Task LoadAsync(string artUrl, string key)
    {
        byte[]? bytes = null;

        // A download the settings currently forbid is not remembered, so switching the setting on
        // shows the cover right away instead of keeping a cached failure.
        bool remember = true;

        try
        {
            string? path = ToLocalPath(artUrl);

            if (path is not null)
            {
                bytes = await ReadFileAsync(path).ConfigureAwait(false);
            }
            else if (settings.HttpArtwork)
            {
                bytes = await DownloadAsync(artUrl).ConfigureAwait(false);
            }
            else
            {
                remember = false;
            }
        }
        catch (Exception ex)
        {
            logger.Warn($"MPRIS: cannot load the artwork {artUrl}: {ex.Message}");
        }
        finally
        {
            if (remember)
            {
                Store(key, bytes);
            }
            else
            {
                Forget(key);
            }
        }

        if (bytes is not null)
        {
            RaiseArtworkLoaded();
        }
    }

    private static async Task<byte[]?> ReadFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        FileInfo info = new(path);
        if (info.Length is 0 or > MaximumImageBytes)
        {
            return null;
        }

        return await File.ReadAllBytesAsync(path).ConfigureAwait(false);
    }

    private async Task<byte[]?> DownloadAsync(string artUrl)
    {
        if (!Uri.TryCreate(artUrl, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        HttpClient http = _http ??= new HttpClient { Timeout = HttpTimeout };
        using HttpResponseMessage response = await http.GetAsync(uri).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaximumImageBytes)
        {
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }

    /// <summary>Stores the result, including a failed load, so a broken URL is not retried on every frame.</summary>
    private void Store(string key, byte[]? bytes)
    {
        lock (_gate)
        {
            _loading.Remove(key);

            if (_disposed)
            {
                return;
            }

            _entries[key] = bytes ?? [];
            _bytes += bytes?.LongLength ?? 0;
            _order.AddLast(key);

            while (_order.Count > MaximumEntries || _bytes > MaximumBytes)
            {
                LinkedListNode<string>? oldest = _order.First;
                if (oldest is null)
                {
                    break;
                }

                _order.RemoveFirst();

                if (_entries.Remove(oldest.Value, out byte[]? removed))
                {
                    _bytes -= removed.LongLength;
                }
            }
        }
    }

    /// <summary>Drops a pending load without caching its result.</summary>
    private void Forget(string key)
    {
        lock (_gate)
        {
            _loading.Remove(key);
        }
    }

    /// <summary>Empties the cache, for example after the artwork settings changed.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _order.Clear();
            _bytes = 0;
        }
    }

    /// <summary>Moves an entry to the end of the LRU order.</summary>
    private void Touch(string key)
    {
        LinkedListNode<string>? node = _order.Find(key);
        if (node is null)
        {
            return;
        }

        _order.Remove(node);
        _order.AddLast(node);
    }

    private void RaiseArtworkLoaded()
    {
        try
        {
            ArtworkLoaded?.Invoke();
        }
        catch (Exception ex)
        {
            logger.Error("MPRIS: an artwork handler failed.", ex);
        }
    }
}
