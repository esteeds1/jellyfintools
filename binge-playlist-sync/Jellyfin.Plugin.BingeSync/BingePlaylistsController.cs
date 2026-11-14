using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.BingeSync;

[ApiController]
[Authorize]
[Route("BingeSync/v1/Playlists")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BingePlaylistsController(PlaylistStore store) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Snapshot>> Get([FromQuery] string? revision, CancellationToken cancellationToken)
    {
        if (UserId() is not { } userId)
        {
            return Forbid();
        }

        var snapshot = await store.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        return revision is not null && string.Equals(revision, snapshot.Revision, StringComparison.Ordinal)
            ? NoContent()
            : Ok(snapshot);
    }

    [HttpPut]
    [RequestSizeLimit(PlaylistStore.MaxBytes)]
    public async Task<ActionResult<Snapshot>> Put([FromBody] PlaylistWrite write, CancellationToken cancellationToken)
    {
        if (UserId() is not { } userId)
        {
            return Forbid();
        }

        if (!PlaylistStore.IsValid(write))
        {
            return BadRequest("Invalid playlist snapshot.");
        }

        try
        {
            var snapshot = await store.PutAsync(userId, write, cancellationToken).ConfigureAwait(false);
            return snapshot is null ? Conflict() : Ok(snapshot);
        }
        catch (PlaylistSizeException)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
    }

    private Guid? UserId()
    {
        // Jellyfin's auth handler sets these claims from the validated session.
        if (bool.TryParse(User.FindFirst("Jellyfin-IsApiKey")?.Value, out var isApiKey) && isApiKey)
        {
            return null;
        }

        return Guid.TryParse(User.FindFirst("Jellyfin-UserId")?.Value, out var userId) && userId != Guid.Empty
            ? userId
            : null;
    }
}
