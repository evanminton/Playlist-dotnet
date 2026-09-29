using PlaylistImport.Formats.Csv;

namespace PlaylistImport.Tests;

public class CsvPlaylistImporterTests
{
    private readonly CsvPlaylistImporter _importer = new();

    [Fact]
    public async Task Maps_aliased_columns_and_detects_semicolons()
    {
        await using var stream = Sample.Open("export.csv");
        var playlist = Assert.Single(await _importer.ImportAsync(stream, "export.csv", TestContext.Current.CancellationToken));

        Assert.Equal("export", playlist.Name);
        Assert.Equal(3, playlist.Tracks.Count); // blank row skipped

        var strobe = playlist.Tracks[0];
        Assert.Equal("Strobe", strobe.Title);
        Assert.Equal("deadmau5", strobe.Artist);
        Assert.Equal(TimeSpan.FromSeconds(637), strobe.Duration); // milliseconds column
        Assert.Equal(128, strobe.Bpm);
        Assert.Equal("Bbm", strobe.Key);
        Assert.Equal("/music/strobe.mp3", strobe.Location);

        var opus = playlist.Tracks[1];
        Assert.Equal("Opus; Extended", opus.Title);
        Assert.Equal("Prydz, Eric", opus.Artist);
        Assert.Equal(TimeSpan.FromSeconds(543), opus.Duration);
        Assert.Equal(126.5, opus.Bpm);
        Assert.Null(opus.Location);

        var quoted = playlist.Tracks[2];
        Assert.Equal("Line\nBreak \"Quoted\"", quoted.Title);
        Assert.Equal(3, quoted.Position);
        Assert.Equal(new TimeSpan(1, 2, 3), quoted.Duration);
    }

    [Fact]
    public async Task Reads_comma_delimited()
    {
        await using var stream = Sample.FromText("Title,Artist,Time\nOne,A,3:30\nTwo,B,215\n");
        var playlist = Assert.Single(await _importer.ImportAsync(stream, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Untitled", playlist.Name);
        Assert.Equal(["One", "Two"], playlist.Tracks.Select(t => t.Title));
        Assert.Equal([TimeSpan.FromSeconds(210), TimeSpan.FromSeconds(215)], playlist.Tracks.Select(t => t.Duration!.Value));
    }

    [Fact]
    public async Task Rejects_header_without_title_or_location()
    {
        await using var stream = Sample.FromText("foo,bar\n1,2\n");
        await Assert.ThrowsAsync<PlaylistImportException>(() => _importer.ImportAsync(stream, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("3:45", 225)]
    [InlineData("1:00:00", 3600)]
    [InlineData("200", 200)]
    [InlineData("200000", 200)]
    [InlineData("abc", null)]
    [InlineData("", null)]
    public void Parses_durations(string value, int? expectedSeconds) =>
        Assert.Equal(expectedSeconds is { } s ? TimeSpan.FromSeconds(s) : null, CsvPlaylistImporter.ParseDuration(value));
}
