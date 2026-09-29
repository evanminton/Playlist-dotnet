using PlaylistImport.Formats.Csv;
using PlaylistImport.Formats.M3u;
using PlaylistImport.Formats.Rekordbox;
using PlaylistImport.Model;

namespace PlaylistImport.Tests;

public class RoundTripTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly Playlist Sample1 = new()
    {
        Name = "Friday, \"Late\" Set",
        FolderPath = ["Gigs", "2026"],
        Source = PlaylistFormat.Csv,
        Tracks =
        [
            new() { Position = 1, Title = "Strobe", Artist = "deadmau5", Album = "For Lack of a Better Name", Genre = "Progressive House",
                    Duration = TimeSpan.FromSeconds(637), Bpm = 128, Key = "Bbm", Location = "/Users/dj/Music/Strobe (Club Mix).mp3" },
            new() { Position = 2, Title = "Opus, Extended", Artist = "Prydz, Eric", Album = "Opus", Duration = new TimeSpan(1, 2, 3),
                    Bpm = 126.5, Key = "4A", Location = "C:/Music/Ünïcode & \"quotes\".flac" },
            new() { Position = 3, Title = "Line\nBreak", Location = "C:/Music/demo.wav" },
        ],
    };

    [Fact]
    public async Task Csv_round_trips_track_fields()
    {
        var back = Assert.Single(await RoundTrip(new CsvPlaylistExporter(), new CsvPlaylistImporter(), [Sample1]));

        AssertTracksEqual(Sample1.Tracks, back.Tracks);
    }

    [Fact]
    public async Task M3u_round_trips_name_and_core_fields()
    {
        var back = Assert.Single(await RoundTrip(new M3uPlaylistExporter(), new M3uPlaylistImporter(), [Sample1]));

        Assert.Equal(Sample1.Name, back.Name);
        Assert.Equal(Sample1.Tracks.Count, back.Tracks.Count);
        for (var i = 0; i < back.Tracks.Count; i++)
        {
            var (expected, actual) = (Sample1.Tracks[i], back.Tracks[i]);
            Assert.Equal(expected.Location, actual.Location);
            Assert.Equal(expected.Duration, actual.Duration);
            Assert.Equal(expected.Album, actual.Album);
            Assert.Equal(expected.Artist, actual.Artist);
            // M3U is line-based, so newlines in titles become spaces.
            Assert.Equal(expected.Title?.ReplaceLineEndings(" "), actual.Title);
        }
    }

    [Fact]
    public async Task Rekordbox_round_trips_library()
    {
        await using var input = Sample.Open("rekordbox.xml");
        var original = await new RekordboxXmlImporter().ImportAsync(input, cancellationToken: Ct);

        var back = await RoundTrip(new RekordboxXmlExporter(), new RekordboxXmlImporter(), original);

        Assert.Equal(original.Select(p => (p.Name, string.Join('/', p.FolderPath))), back.Select(p => (p.Name, string.Join('/', p.FolderPath))));
        for (var i = 0; i < original.Count; i++)
            AssertTracksEqual(original[i].Tracks, back[i].Tracks);
    }

    [Fact]
    public async Task Rekordbox_shares_collection_entries_between_playlists()
    {
        var other = Sample1 with { Name = "Other", FolderPath = [], Tracks = [Sample1.Tracks[0] with { Position = 1 }] };
        using var stream = new MemoryStream();
        await new RekordboxXmlExporter().ExportAsync([Sample1, other], stream, Ct);

        var xml = System.Xml.Linq.XDocument.Parse(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        Assert.Equal(3, xml.Root!.Element("COLLECTION")!.Elements("TRACK").Count());

        stream.Position = 0;
        var back = await new RekordboxXmlImporter().ImportAsync(stream, cancellationToken: Ct);
        Assert.Equal(["Gigs", "2026"], back[0].FolderPath);
        AssertTracksEqual(Sample1.Tracks, back[0].Tracks);
        AssertTracksEqual(other.Tracks, back[1].Tracks);
    }

    [Fact]
    public async Task Converts_rekordbox_playlist_to_m3u()
    {
        var exporter = new PlaylistExporter();
        var importer = new PlaylistImporter();
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var library = await importer.ImportFileAsync(Sample.Path("rekordbox.xml"), Ct);
            var path = Path.Combine(dir.FullName, "warmup.m3u8");
            await exporter.ExportFileAsync(path, library[0], Ct);

            var back = Assert.Single(await importer.ImportFileAsync(path, Ct));
            Assert.Equal("Warmup", back.Name);
            Assert.Equal(["Opus", "Strobe"], back.Tracks.Select(t => t.Title));
            Assert.Equal("C:/Music/Opus.flac", back.Tracks[0].Location);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(PlaylistFormat.Csv)]
    [InlineData(PlaylistFormat.M3u)]
    public async Task Single_playlist_formats_reject_several(PlaylistFormat format)
    {
        using var stream = new MemoryStream();
        await Assert.ThrowsAsync<ArgumentException>(() => new PlaylistExporter().ExportAsync([Sample1, Sample1], stream, format, Ct));
    }

    [Fact]
    public async Task M3u_skips_tracks_without_location()
    {
        var playlist = Sample1 with { Tracks = [new PlaylistTrack { Position = 1, Title = "Nowhere" }, Sample1.Tracks[2] with { Position = 2 }] };
        var back = Assert.Single(await RoundTrip(new M3uPlaylistExporter(), new M3uPlaylistImporter(), [playlist]));

        Assert.Equal(["C:/Music/demo.wav"], back.Tracks.Select(t => t.Location));
    }

    [Theory]
    [InlineData(@"C:\Music\a b.mp3", "file://localhost/C:/Music/a%20b.mp3")]
    [InlineData("/Users/me/テ.mp3", "file://localhost/Users/me/%E3%83%86.mp3")]
    [InlineData("https://example.com/x.mp3", "https://example.com/x.mp3")]
    public void Encodes_rekordbox_locations(string path, string expected) =>
        Assert.Equal(expected, RekordboxXmlExporter.EncodeLocation(path));

    [Theory]
    [InlineData(59, "0:59")]
    [InlineData(637, "10:37")]
    [InlineData(3723, "1:02:03")]
    public void Formats_csv_durations(int seconds, string expected) =>
        Assert.Equal(expected, CsvPlaylistExporter.FormatDuration(TimeSpan.FromSeconds(seconds)));

    private static async Task<IReadOnlyList<Playlist>> RoundTrip(IPlaylistExporter exporter, IPlaylistImporter importer, IReadOnlyList<Playlist> playlists)
    {
        using var stream = new MemoryStream();
        await exporter.ExportAsync(playlists, stream, Ct);
        stream.Position = 0;
        return await importer.ImportAsync(stream, cancellationToken: Ct);
    }

    // SourceId and Source are assigned by the reader, so they are not expected to survive a round trip.
    private static void AssertTracksEqual(IReadOnlyList<PlaylistTrack> expected, IReadOnlyList<PlaylistTrack> actual) =>
        Assert.Equal(expected.Select(t => t with { SourceId = null }), actual.Select(t => t with { SourceId = null }));
}
