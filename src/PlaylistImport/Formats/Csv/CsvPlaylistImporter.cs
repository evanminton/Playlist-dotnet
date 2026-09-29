using System.Globalization;
using PlaylistImport.Model;

namespace PlaylistImport.Formats.Csv;

/// <summary>
/// Reads a playlist from a delimited text file with a header row. Columns are matched by name
/// (case-insensitive, common aliases such as "Track Name" or "Artist Name(s)" accepted), so exports
/// from Spotify tools, Excel, Serato or Traktor all work without configuration. The delimiter
/// (comma, semicolon or tab) is detected from the header row.
/// </summary>
public sealed class CsvPlaylistImporter : IPlaylistImporter
{
    private static readonly Dictionary<string, Field> ColumnAliases = BuildAliases();

    public PlaylistFormat Format => PlaylistFormat.Csv;

    public IReadOnlyCollection<string> Extensions { get; } = [".csv", ".tsv"];

    public async Task<IReadOnlyList<Playlist>> ImportAsync(Stream stream, string? sourceName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);

        var delimiter = DetectDelimiter(text);
        using var rows = CsvReader.ReadRows(text, delimiter).GetEnumerator();

        if (!rows.MoveNext())
            throw new PlaylistImportException("CSV file is empty.");

        var columns = MapHeader(rows.Current);
        if (!columns.ContainsKey(Field.Title) && !columns.ContainsKey(Field.Location))
            throw new PlaylistImportException("CSV header has neither a title nor a location column.");

        var tracks = new List<PlaylistTrack>();
        while (rows.MoveNext())
        {
            var row = rows.Current;
            if (row.All(string.IsNullOrWhiteSpace))
                continue;

            string? Get(Field f) =>
                columns.TryGetValue(f, out var i) && i < row.Count && !string.IsNullOrWhiteSpace(row[i]) ? row[i].Trim() : null;

            tracks.Add(new PlaylistTrack
            {
                Position = tracks.Count + 1,
                Title = Get(Field.Title),
                Artist = Get(Field.Artist),
                Album = Get(Field.Album),
                Genre = Get(Field.Genre),
                Duration = ParseDuration(Get(Field.Duration)),
                Bpm = ParseNumber(Get(Field.Bpm)),
                Key = Get(Field.Key),
                Location = Get(Field.Location),
            });
        }

        return
        [
            new Playlist
            {
                Name = M3u.M3uPlaylistImporter.NameFromSource(sourceName),
                Tracks = tracks,
                Source = Format,
            },
        ];
    }

    /// <summary>
    /// Parses "m:ss", "h:mm:ss", plain seconds, or milliseconds (values over 10,000 with no colon, as Spotify exports write).
    /// </summary>
    internal static TimeSpan? ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (value.Contains(':'))
        {
            var parts = value.Split(':');
            double total = 0;
            foreach (var part in parts)
            {
                if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || n < 0)
                    return null;
                total = total * 60 + n;
            }
            return TimeSpan.FromSeconds(total);
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || number < 0)
            return null;

        return number > 10_000 ? TimeSpan.FromMilliseconds(number) : TimeSpan.FromSeconds(number);
    }

    // Semicolon-delimited exports from European locales write decimals with a comma.
    private static double? ParseNumber(string? value) =>
        double.TryParse(value?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static char DetectDelimiter(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        var header = end >= 0 ? text[..end] : text;

        char[] candidates = [',', ';', '\t'];
        return candidates.MaxBy(c => header.Count(ch => ch == c));
    }

    private static Dictionary<Field, int> MapHeader(IReadOnlyList<string> header)
    {
        var map = new Dictionary<Field, int>();
        for (var i = 0; i < header.Count; i++)
        {
            if (ColumnAliases.TryGetValue(Normalize(header[i]), out var field))
                map.TryAdd(field, i);
        }
        return map;
    }

    private static string Normalize(string column) =>
        new(column.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static Dictionary<string, Field> BuildAliases()
    {
        var aliases = new (Field Field, string[] Names)[]
        {
            (Field.Title, ["title", "name", "track", "trackname", "song", "songname"]),
            (Field.Artist, ["artist", "artists", "artistname", "artistnames"]),
            (Field.Album, ["album", "albumname", "release"]),
            (Field.Genre, ["genre", "genres"]),
            (Field.Duration, ["duration", "time", "length", "durationms", "tracklength", "totaltime"]),
            (Field.Bpm, ["bpm", "tempo", "averagebpm"]),
            (Field.Key, ["key", "musicalkey", "tonality", "initialkey"]),
            (Field.Location, ["location", "path", "file", "filepath", "filename", "url", "uri", "trackuri", "spotifyuri"]),
        };

        var dict = new Dictionary<string, Field>();
        foreach (var (field, names) in aliases)
            foreach (var name in names)
                dict.TryAdd(name, field);
        return dict;
    }

    private enum Field { Title, Artist, Album, Genre, Duration, Bpm, Key, Location }
}
