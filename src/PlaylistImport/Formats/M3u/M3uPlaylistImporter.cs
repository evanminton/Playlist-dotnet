using System.Globalization;
using PlaylistImport.Model;

namespace PlaylistImport.Formats.M3u;

/// <summary>
/// Reads plain and extended M3U / M3U8 playlists. Recognises <c>#EXTINF</c> (duration and "Artist - Title"),
/// <c>#PLAYLIST</c> (name), <c>#EXTALB</c> and <c>#EXTART</c>; other directives are ignored.
/// </summary>
public sealed class M3uPlaylistImporter : IPlaylistImporter
{
    public PlaylistFormat Format => PlaylistFormat.M3u;

    public IReadOnlyCollection<string> Extensions { get; } = [".m3u", ".m3u8"];

    public async Task<IReadOnlyList<Playlist>> ImportAsync(Stream stream, string? sourceName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // M3U8 is UTF-8 by definition; plain M3U in the wild is almost always UTF-8 too, and a BOM wins if present.
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        var tracks = new List<PlaylistTrack>();
        string? name = null;
        PendingInfo pending = default;

        while (await reader.ReadLineAsync(cancellationToken) is { } raw)
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith('#'))
            {
                if (TryDirective(line, "#EXTINF:", out var extinf))
                    pending = pending with { Info = extinf };
                else if (TryDirective(line, "#PLAYLIST:", out var playlistName))
                    name = playlistName.Trim();
                else if (TryDirective(line, "#EXTALB:", out var album))
                    pending = pending with { Album = album.Trim() };
                else if (TryDirective(line, "#EXTART:", out var artist))
                    pending = pending with { Artist = artist.Trim() };
                continue;
            }

            tracks.Add(BuildTrack(tracks.Count + 1, line, pending));
            pending = default;
        }

        return
        [
            new Playlist
            {
                Name = name ?? NameFromSource(sourceName),
                Tracks = tracks,
                Source = Format,
            },
        ];
    }

    private static PlaylistTrack BuildTrack(int position, string location, PendingInfo pending)
    {
        TimeSpan? duration = null;
        string? artist = pending.Artist;
        string? title = null;

        if (pending.Info is { } info)
        {
            // #EXTINF:<seconds>[ attr="..."],<display>
            var comma = info.IndexOf(',');
            var head = comma >= 0 ? info[..comma] : info;
            var display = comma >= 0 ? info[(comma + 1)..].Trim() : null;

            var secondsToken = head.Split(' ', 2)[0];
            if (double.TryParse(secondsToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
                duration = TimeSpan.FromSeconds(seconds);

            if (!string.IsNullOrEmpty(display))
            {
                var dash = display.IndexOf(" - ", StringComparison.Ordinal);
                if (dash > 0)
                {
                    artist ??= display[..dash].Trim();
                    title = display[(dash + 3)..].Trim();
                }
                else
                {
                    title = display;
                }
            }
        }

        return new PlaylistTrack
        {
            Position = position,
            Title = title ?? Path.GetFileNameWithoutExtension(location.Replace('\\', '/')),
            Artist = artist,
            Album = pending.Album,
            Duration = duration,
            Location = location,
        };
    }

    private static bool TryDirective(string line, string prefix, out string value)
    {
        if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = line[prefix.Length..];
            return true;
        }

        value = "";
        return false;
    }

    internal static string NameFromSource(string? sourceName) =>
        string.IsNullOrWhiteSpace(sourceName) ? "Untitled" : Path.GetFileNameWithoutExtension(sourceName);

    private readonly record struct PendingInfo(string? Info, string? Artist, string? Album);
}
