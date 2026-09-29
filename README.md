# PlaylistImport

.NET 10 library that reads playlists from **Rekordbox XML**, **CSV/TSV** and **M3U/M3U8** into one model.

```csharp
var importer = new PlaylistImporter();
IReadOnlyList<Playlist> playlists = await importer.ImportFileAsync("rekordbox.xml");

foreach (var p in playlists)
    Console.WriteLine($"{string.Join('/', p.FolderPath)}/{p.Name}: {p.Tracks.Count} tracks");
```

Or from a stream: `importer.ImportAsync(stream, PlaylistFormat.Csv, "export.csv")`.

## Model

`Playlist` has `Name`, `FolderPath` (Rekordbox folders), `Tracks` and `Source`.
`PlaylistTrack` has `Position` plus optional `Title`, `Artist`, `Album`, `Genre`, `Duration`, `Bpm`, `Key`, `Location` and `SourceId`.

## Formats

| Format | Extensions | Notes |
|---|---|---|
| Rekordbox XML | `.xml` | "Export Collection in xml format". One `Playlist` per playlist node; entries resolved by TrackID or Location; `file://localhost/` URIs decoded to paths; DTDs refused. |
| CSV | `.csv`, `.tsv` | Header row required. Columns matched by name with aliases (`Track Name`, `Artist Name(s)`, `Duration (ms)`, `File Path`, ...). Delimiter `,` `;` or tab auto-detected. Durations as `m:ss`, `h:mm:ss`, seconds or milliseconds. |
| M3U | `.m3u`, `.m3u8` | `#EXTINF` duration and `Artist - Title`, `#PLAYLIST`, `#EXTALB`, `#EXTART`. |

Add a format by implementing `IPlaylistImporter` and passing it to `new PlaylistImporter(importers)`.

## Build and test

```sh
dotnet test
```

Tests use xUnit v3 on Microsoft.Testing.Platform (opted in via `global.json`).
