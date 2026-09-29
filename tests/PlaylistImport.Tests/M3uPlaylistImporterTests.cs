using PlaylistImport.Formats.M3u;

namespace PlaylistImport.Tests;

public class M3uPlaylistImporterTests
{
    private readonly M3uPlaylistImporter _importer = new();

    [Fact]
    public async Task Reads_extended_m3u()
    {
        await using var stream = Sample.Open("set.m3u8");
        var playlist = Assert.Single(await _importer.ImportAsync(stream, "set.m3u8", TestContext.Current.CancellationToken));

        Assert.Equal("Friday Set", playlist.Name);
        Assert.Equal(4, playlist.Tracks.Count);

        var strobe = playlist.Tracks[0];
        Assert.Equal(("deadmau5", "Strobe"), (strobe.Artist, strobe.Title));
        Assert.Equal(TimeSpan.FromSeconds(637), strobe.Duration);
        Assert.Equal("/Users/dj/Music/Strobe.mp3", strobe.Location);

        var stream2 = playlist.Tracks[1];
        Assert.Equal("Stream Only", stream2.Title);
        Assert.Null(stream2.Artist);
        Assert.Null(stream2.Duration); // -1 means unknown

        var opus = playlist.Tracks[2];
        Assert.Equal("Opus", opus.Album);
        Assert.Equal(@"C:\Music\Opus.flac", opus.Location);

        var bare = playlist.Tracks[3];
        Assert.Equal(4, bare.Position);
        Assert.Equal("No Info", bare.Title); // falls back to file name
        Assert.Null(bare.Duration);
    }

    [Fact]
    public async Task Uses_file_name_when_playlist_has_no_name()
    {
        await using var stream = Sample.FromText("a.mp3\nb.mp3\n");
        var playlist = Assert.Single(await _importer.ImportAsync(stream, "My Mix.m3u", TestContext.Current.CancellationToken));

        Assert.Equal("My Mix", playlist.Name);
        Assert.Equal(["a", "b"], playlist.Tracks.Select(t => t.Title));
    }
}
