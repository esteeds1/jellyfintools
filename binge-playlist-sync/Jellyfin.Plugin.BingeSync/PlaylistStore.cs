using System.Text.Json;
using System.Text.Json.Serialization;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.BingeSync;

public sealed record Snapshot(
    [property: JsonPropertyName("revision")] string? Revision,
    [property: JsonPropertyName("playlists")] JsonElement[] Playlists,
    [property: JsonPropertyName("deletedPlaylistIds")] string[] DeletedPlaylistIds);

public sealed record PlaylistWrite(
    [property: JsonPropertyName("revision")] string? Revision,
    [property: JsonPropertyName("playlists")] JsonElement[] Playlists);

public sealed class PlaylistStore(IApplicationPaths paths) : IDisposable
{
    internal const int MaxBytes = 8 * 1024 * 1024;
    private static readonly Snapshot Empty = new(null, [], []);
    private readonly string _directory = Path.Combine(paths.DataPath, "binge-playlist-sync");
    private readonly SemaphoreSlim _lock = new(1);

    public async Task<Snapshot> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadAsync(UserPath(userId), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Snapshot?> PutAsync(Guid userId, PlaylistWrite write, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = UserPath(userId);
            var current = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(write.Revision, current.Revision, StringComparison.Ordinal))
            {
                return null;
            }

            var active = write.Playlists.Select(PlaylistId).ToHashSet();
            var deleted = current.DeletedPlaylistIds.ToHashSet();
            deleted.UnionWith(current.Playlists.Select(PlaylistId).Where(id => !active.Contains(id)));
            deleted.ExceptWith(active);
            var next = new Snapshot(Guid.NewGuid().ToString("D"), write.Playlists, [.. deleted.Order()]);
            var data = JsonSerializer.SerializeToUtf8Bytes(next);
            if (data.Length > MaxBytes)
            {
                throw new PlaylistSizeException();
            }

            Directory.CreateDirectory(_directory);
            var temporary = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await File.WriteAllBytesAsync(temporary, data, cancellationToken).ConfigureAwait(false);
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }

            return next;
        }
        finally
        {
            _lock.Release();
        }
    }

    internal static bool IsValid(PlaylistWrite write)
    {
        if (write.Revision is not null && !Guid.TryParseExact(write.Revision, "D", out _))
        {
            return false;
        }

        var ids = new HashSet<string>();
        return write.Playlists is not null && write.Playlists.All(playlist =>
            playlist.ValueKind == JsonValueKind.Object
            && playlist.TryGetProperty("id", out var id)
            && id.ValueKind == JsonValueKind.String
            && Guid.TryParseExact(id.GetString(), "D", out var uuid)
            && uuid != Guid.Empty
            && ids.Add(uuid.ToString("D"))
            && playlist.TryGetProperty("name", out var name)
            && name.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(name.GetString())
            && playlist.TryGetProperty("kind", out var kind)
            && kind.ValueKind == JsonValueKind.String
            && kind.GetString() is "movies" or "tv" or "music" or "mixed"
            && playlist.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array);
    }

    private static string PlaylistId(JsonElement playlist) => playlist.GetProperty("id").GetGuid().ToString("D");

    private string UserPath(Guid userId) => Path.Combine(_directory, userId.ToString("N") + ".json");

    private static async Task<Snapshot> ReadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        await using var file = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Snapshot>(file, cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The Binge playlist snapshot is empty.");
    }

    public void Dispose() => _lock.Dispose();
}

internal sealed class PlaylistSizeException : Exception;
