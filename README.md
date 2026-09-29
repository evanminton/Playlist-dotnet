# PlaylistImport

.NET 10 library that reads and writes playlists in **Rekordbox XML**, **CSV** and **M3U/M3U8**, through one model.

```csharp
var importer = new PlaylistImporter();
IReadOnlyList<Playlist> playlists = await importer.ImportFileAsync("rekordbox.xml");

foreach (var p in playlists)
    Console.WriteLine($"{string.Join('/', p.FolderPath)}/{p.Name}: {p.Tracks.Count} tracks");
```

Or from a stream: `importer.ImportAsync(stream, PlaylistFormat.Csv, "export.csv")`.

Export works the same way, so converting between formats is an import followed by an export:

```csharp
var exporter = new PlaylistExporter();
await exporter.ExportFileAsync("warmup.m3u8", playlists[0]);    // one playlist per CSV/M3U file
await exporter.ExportFileAsync("library.xml", playlists);       // Rekordbox XML holds many
```

## Model

`Playlist` has `Name`, `FolderPath` (Rekordbox folders), `Tracks` and `Source`.
`PlaylistTrack` has `Position` plus optional `Title`, `Artist`, `Album`, `Genre`, `Duration`, `Bpm`, `Key`, `Location` and `SourceId`.

## Formats

| Format | Extensions | Notes |
|---|---|---|
| Rekordbox XML | `.xml` | "Export Collection in xml format". One `Playlist` per playlist node; entries resolved by TrackID or Location; `file://localhost/` URIs decoded to paths; DTDs refused. |
| CSV | `.csv`, `.tsv` | Header row required. Columns matched by name with aliases (`Track Name`, `Artist Name(s)`, `Duration (ms)`, `File Path`, ...). Delimiter `,` `;` or tab auto-detected. Durations as `m:ss`, `h:mm:ss`, seconds or milliseconds. |
| M3U | `.m3u`, `.m3u8` | `#EXTINF` duration and `Artist - Title`, `#PLAYLIST`, `#EXTALB`, `#EXTART`. |

### Export

| Format | Writes |
|---|---|
| Rekordbox XML | `COLLECTION` de-duplicated by location, playlists nested by `FolderPath`, entries keyed by TrackID, paths encoded as `file://localhost/` URIs. Load it in Rekordbox via Preferences > Advanced > rekordbox xml. |
| CSV | `Title,Artist,Album,Genre,Duration,BPM,Key,Location`, UTF-8, CRLF, RFC 4180 quoting, durations as `m:ss`. |
| M3U | `#EXTM3U`, `#PLAYLIST`, `#EXTINF`, `#EXTALB`, UTF-8. Tracks without a location are skipped. |

CSV and M3U hold one playlist per file and throw `ArgumentException` if given more.

Add a format by implementing `IPlaylistImporter` / `IPlaylistExporter` and passing it to the `PlaylistImporter` / `PlaylistExporter` constructor.

## Build and test

```sh
dotnet test
```

Tests use xUnit v3 on Microsoft.Testing.Platform (opted in via `global.json`).
