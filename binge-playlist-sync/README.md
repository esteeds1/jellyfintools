# Binge Playlist Sync for Jellyfin

Binge Playlist Sync stores and shares Binge playlists between devices signed in to the same Jellyfin user account. The plugin stores its data on the Jellyfin host and serves it through Jellyfin's existing HTTP interface and user sessions. Binge playlists appear in compatible clients such as MediaHarbour 3.2.5 build 410. They use their own storage and API alongside Jellyfin's built-in playlists.

## Install

The release archive includes builds tested on Jellyfin 12.4.0 and 10.11.11.

1. Stop Jellyfin.
2. Create a `BingePlaylistSync` subdirectory in Jellyfin's plugin directory. The plugin directory is commonly `/var/lib/jellyfin/plugins` for Linux packages or `/config/plugins` inside the official Docker container.
3. Copy the `Jellyfin.Plugin.BingeSync.dll` from the archive's matching `jellyfin-12.4.0` or `jellyfin-10.11.11` folder into that subdirectory.
4. Start Jellyfin and check **Dashboard → Plugins** for **Binge Playlist Sync**.
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

## Licence

GPL-3.0-only. See [LICENSE](LICENSE).
