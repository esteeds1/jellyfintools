# Media Harbour Plugin for Jellyfin

The Media Harbour Plugin stores and shares Binge playlists between devices signed in to the same Jellyfin user account. The plugin stores its data on the Jellyfin host and serves it through Jellyfin's existing HTTP interface and user sessions. Binge playlists appear in compatible clients such as MediaHarbour 3.2.5 build 410. They use their own storage and API alongside Jellyfin's built-in playlists.

## Install

Install **Media Harbour Plugin** from the Jellyfin plugin catalog using the [repository manifest](https://raw.githubusercontent.com/esteeds1/jellyfintools/main/binge-playlist-sync/manifest.json). Builds are available for Jellyfin 12.4.0 and 10.11.11.

For a manual install, download the release ZIP matching your Jellyfin version.

1. Stop Jellyfin.
2. Create a `BingePlaylistSync` subdirectory in Jellyfin's plugin directory. The plugin directory is commonly `/var/lib/jellyfin/plugins` for Linux packages or `/config/plugins` inside the official Docker container.
3. Copy the `Jellyfin.Plugin.BingeSync.dll` from the root of the ZIP into that subdirectory.
4. Start Jellyfin and check **Dashboard → Plugins** for **Media Harbour Plugin**.
5. Open build 410 on each Apple TV and sign in to the same Jellyfin user. Create a Binge playlist on one device and check Binge on the other. Active clients check for changes about every four seconds.

See the [Jellyfin plugin installation docs](https://jellyfin.org/docs/general/server/plugins/) for other platforms. The plugin uses Jellyfin's existing base URL, including any path prefix configured on your reverse proxy.

Existing MediaHarbour playlists cached on the Apple TV are imported during sync. Where a playlist UUID already exists on the server, the server's record takes precedence on first sync. For offline edits, the app keeps a device cache and retries when the server becomes reachable. tvOS may reclaim the cache between launches. The Jellyfin host keeps the synced copy.

## Build from source

Run either command from the source directory with the corresponding .NET SDK installed.

```sh
dotnet build Jellyfin.Plugin.BingeSync -c Release -p:JellyfinVersion=12.4.0
dotnet build Jellyfin.Plugin.BingeSync -c Release -p:JellyfinVersion=10.11.11
```

The plugin DLL is under `bin/Release/net10.0` or `bin/Release/net9.0` inside the project directory.

## API and storage

- `GET /BingeSync/v1/Playlists` returns `revision`, `playlists`, and `deletedPlaylistIds`. A fresh account has a null revision. Passing the latest revision as `?revision=…` returns HTTP 204 when no update is available.
- `PUT /BingeSync/v1/Playlists` accepts a JSON object with `revision` and `playlists`. Use the revision from the last successful response, or omit it for a fresh account. A stale write returns HTTP 409. Fetch the current snapshot, merge pending changes, and retry. Successful writes return the updated snapshot.
- Both routes use Jellyfin's authenticated user session. Data is keyed by the identity provided by Jellyfin. Server API keys have no playlist scope.
- Playlist objects have a UUID `id`, a `name`, a `kind` (`movies`, `tv`, `music`, or `mixed`), and an ordered `items` array. The plugin preserves optional fields such as playlist playback progress and cleanup settings. A write replaces the account's snapshot. Omitted playlist UUIDs are recorded as deletions for clients importing an older copy. The per-user snapshot limit is 8 MiB.
- Include the `binge-playlist-sync` subdirectory of Jellyfin's data path (`IApplicationPaths.DataPath`) when backing up Binge playlists.

## IPTV sync in version 1.1.0

Build from these sources for the matching Jellyfin target, then replace the existing
plug-in DLL while Jellyfin is stopped. Remove any duplicate older installation
before restarting.

In MediaHarbour 3.2.6 build 479, enable **Sync IPTV Favourites and Recently Watched**
under **Settings → Media Harbour Plugin** on each Apple TV. Use the same Jellyfin
server address and account, and the same M3U playlist on every device for direct
IPTV. Channel IDs must match, including the stream URLs used to derive them. The
app uploads only SHA-256 channel identifiers and watch timestamps. Playlist URLs,
credentials, and guide sources are configured separately on each device.

The authenticated `GET` and `PUT /BingeSync/v1/IPTV` routes use a separate
per-user revision and store. A stale write returns HTTP 409. Reverse proxies
must forward this route as well as the existing playlist route.

## Licence

GPL-3.0-only. See [LICENSE](LICENSE).
