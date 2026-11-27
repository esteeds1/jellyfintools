using System.Text.Json;
using System.Text.Json.Serialization;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.BingeSync;

public sealed record IptvSnapshot(
    [property: JsonPropertyName("revision")] string? Revision,
    [property: JsonPropertyName("favourites")] string[] Favourites,
    [property: JsonPropertyName("recent")] Dictionary<string, double> Recent);

public sealed class IptvStore(IApplicationPaths paths) : IDisposable
{
    private readonly string _directory = Path.Combine(paths.DataPath, "binge-playlist-sync", "iptv");
    private readonly SemaphoreSlim _lock = new(1);

    public async Task<IptvSnapshot?> ExchangeAsync(Guid userId, IptvSnapshot? write, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = Path.Combine(_directory, userId.ToString("N") + ".json");
            var current = File.Exists(path)
                ? JsonSerializer.Deserialize<IptvSnapshot>(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false))
                    ?? throw new InvalidDataException("Missing IPTV snapshot.")
                : new IptvSnapshot(null, [], new Dictionary<string, double>());
            if (write is null) return current;
            if (!string.Equals(current.Revision, write.Revision, StringComparison.Ordinal)) return null;

            var next = write with { Revision = Guid.NewGuid().ToString("D") };
            Directory.CreateDirectory(_directory);
            var temp = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await File.WriteAllBytesAsync(temp, JsonSerializer.SerializeToUtf8Bytes(next), cancellationToken).ConfigureAwait(false);
                File.Move(temp, path, overwrite: true);
            }
            finally
            {
                File.Delete(temp);
            }
            return next;
        }
        finally
        {
            _lock.Release();
        }
    }

    internal static bool IsValid(IptvSnapshot write) =>
        write.Favourites is { Length: <= 50000 } && write.Favourites.All(IsHash)
        && write.Recent is { Count: <= 100 }
        && write.Recent.All(entry => IsHash(entry.Key) && double.IsFinite(entry.Value)
            && entry.Value >= 0 && entry.Value <= 253402300799);

    private static bool IsHash(string? key)
    {
        Span<byte> bytes = stackalloc byte[32];
        return key is { Length: 44 } && Convert.TryFromBase64String(key, bytes, out var count) && count == 32
            && Convert.ToBase64String(bytes) == key;
    }

    public void Dispose() => _lock.Dispose();
}
