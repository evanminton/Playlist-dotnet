using System.Globalization;
using System.Text;
using PlaylistImport.Model;

namespace PlaylistImport.Formats.M3u;

/// <summary>
/// Writes an extended M3U playlist (UTF-8, no BOM) with <c>#PLAYLIST</c>, <c>#EXTINF</c> and <c>#EXTALB</c>.
/// Tracks without a <see cref="PlaylistTrack.Location"/> are skipped, since an M3U entry is its location.
/// </summary>
public sealed class M3uPlaylistExporter : IPlaylistExporter
{
    public PlaylistFormat Format => PlaylistFormat.M3u;

    public IReadOnlyCollection<string> Extensions { get; } = [".m3u8", ".m3u"];

    public bool SupportsMultiplePlaylists => false;

    public async Task ExportAsync(IReadOnlyList<Playlist> playlists, Stream stream, CancellationToken cancellationToken = default)
    {
        var playlist = ExportGuard.Single(playlists, Format);
        ArgumentNullException.ThrowIfNull(stream);

        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\n" };

        await writer.WriteLineAsync("#EXTM3U");
        await writer.WriteLineAsync($"#PLAYLIST:{OneLine(playlist.Name)}");

        foreach (var track in playlist.Tracks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(track.Location))
                continue;

            var seconds = track.Duration is { } d ? ((long)Math.Round(d.TotalSeconds)).ToString(CultureInfo.InvariantCulture) : "-1";
            var display = (track.Artist, track.Title) switch
            {
                ({ Length: > 0 } a, { Length: > 0 } t) => $"{a} - {t}",
                (_, { Length: > 0 } t) => t,
                ({ Length: > 0 } a, _) => a,
                _ => "",
            };

            await writer.WriteLineAsync($"#EXTINF:{seconds},{OneLine(display)}");
            if (!string.IsNullOrEmpty(track.Album))
                await writer.WriteLineAsync($"#EXTALB:{OneLine(track.Album)}");
            await writer.WriteLineAsync(OneLine(track.Location));
        }
    }

    // M3U is line-based, so embedded newlines would corrupt the file.
    private static string OneLine(string value) => value.ReplaceLineEndings(" ");
}
