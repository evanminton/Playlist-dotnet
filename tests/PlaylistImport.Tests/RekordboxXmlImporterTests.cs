using PlaylistImport.Formats.Rekordbox;
using PlaylistImport.Model;

namespace PlaylistImport.Tests;

public class RekordboxXmlImporterTests
{
    private readonly RekordboxXmlImporter _importer = new();

    [Fact]
    public async Task Imports_every_playlist_with_folder_path()
    {
        await using var stream = Sample.Open("rekordbox.xml");
        var playlists = await _importer.ImportAsync(stream, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Collection(playlists,
            p =>
            {
                Assert.Equal("Warmup", p.Name);
                Assert.Empty(p.FolderPath);
            },
            p =>
            {
                Assert.Equal("Saturday", p.Name);
                Assert.Equal(["Gigs", "2026"], p.FolderPath);
            });
        Assert.All(playlists, p => Assert.Equal(PlaylistFormat.RekordboxXml, p.Source));
    }

    [Fact]
    public async Task Resolves_tracks_by_id_in_playlist_order()
    {
        await using var stream = Sample.Open("rekordbox.xml");
        var warmup = (await _importer.ImportAsync(stream, cancellationToken: TestContext.Current.CancellationToken))[0];

        Assert.Equal(["Opus", "Strobe"], warmup.Tracks.Select(t => t.Title));

        var strobe = warmup.Tracks[1];
        Assert.Equal(2, strobe.Position);
        Assert.Equal("deadmau5", strobe.Artist);
        Assert.Equal("For Lack of a Better Name", strobe.Album);
        Assert.Equal("Progressive House", strobe.Genre);
        Assert.Equal(TimeSpan.FromSeconds(637), strobe.Duration);
        Assert.Equal(128.0, strobe.Bpm);
        Assert.Equal("Bbm", strobe.Key);
        Assert.Equal("101", strobe.SourceId);
        Assert.Equal("/Users/dj/Music/Strobe (Club Mix).mp3", strobe.Location);
    }

    [Fact]
    public async Task Resolves_tracks_by_location_and_keeps_dangling_entries()
    {
        await using var stream = Sample.Open("rekordbox.xml");
        var saturday = (await _importer.ImportAsync(stream, cancellationToken: TestContext.Current.CancellationToken))[1];

        Assert.Equal(2, saturday.Tracks.Count);

        var demo = saturday.Tracks[0];
        Assert.Equal("Untitled Demo", demo.Title);
        Assert.Null(demo.Artist); // empty attribute
        Assert.Null(demo.Bpm);    // 0.00 means not analysed

        var missing = saturday.Tracks[1];
        Assert.Null(missing.Title);
        Assert.Equal("file://localhost/C:/Music/missing.mp3", missing.SourceId);
    }

    [Theory]
    [InlineData("file://localhost/C:/Music/a%20b.mp3", "C:/Music/a b.mp3")]
    [InlineData("file://localhost/Users/me/%E3%83%86.mp3", "/Users/me/テ.mp3")]
    [InlineData("https://example.com/x.mp3", "https://example.com/x.mp3")]
    public void Decodes_locations(string raw, string expected) =>
        Assert.Equal(expected, RekordboxXmlImporter.DecodeLocation(raw));

    [Fact]
    public async Task Rejects_non_rekordbox_xml()
    {
        await using var stream = Sample.FromText("<playlist/>");
        await Assert.ThrowsAsync<PlaylistImportException>(() => _importer.ImportAsync(stream, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rejects_dtd()
    {
        const string xml = """<?xml version="1.0"?><!DOCTYPE x [<!ENTITY a "aaaa">]><DJ_PLAYLISTS/>""";
        await using var stream = Sample.FromText(xml);
        await Assert.ThrowsAsync<PlaylistImportException>(() => _importer.ImportAsync(stream, cancellationToken: TestContext.Current.CancellationToken));
    }
}
