using System.Globalization;
using System.Text;
using PlaylistImport.Model;

namespace PlaylistImport.Formats.Csv;

/// <summary>
/// Writes a playlist as RFC 4180 CSV (UTF-8, no BOM, CRLF) with the header
/// <c>Title,Artist,Album,Genre,Duration,BPM,Key,Location</c>. Durations are written as <c>m:ss</c> or <c>h:mm:ss</c>.
/// </summary>
public sealed class CsvPlaylistExporter : IPlaylistExporter
{
    private static readonly string[] Header = ["Title", "Artist", "Album", "Genre", "Duration", "BPM", "Key", "Location"];

    public PlaylistFormat Format => PlaylistFormat.Csv;

    public IReadOnlyCollection<string> Extensions { get; } = [".csv"];

    public bool SupportsMultiplePlaylists => false;

    public async Task ExportAsync(IReadOnlyList<Playlist> playlists, Stream stream, CancellationToken cancellationToken = default)
    {
        var playlist = ExportGuard.Single(playlists, Format);
        ArgumentNullException.ThrowIfNull(stream);

        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\r\n" };

        await writer.WriteLineAsync(string.Join(',', Header));
        foreach (var t in playlist.Tracks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string?[] fields =
            [
                t.Title, t.Artist, t.Album, t.Genre,
                t.Duration is { } d ? FormatDuration(d) : null,
                t.Bpm?.ToString("0.##", CultureInfo.InvariantCulture),
                t.Key, t.Location,
            ];
            await writer.WriteLineAsync(string.Join(',', fields.Select(Quote)));
        }
    }

    internal static string FormatDuration(TimeSpan duration)
    {
        var total = (long)Math.Round(duration.TotalSeconds);
        var (h, m, s) = (total / 3600, total / 60 % 60, total % 60);
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
    }

    private static string Quote(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        return value.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
