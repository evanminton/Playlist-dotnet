using PlaylistImport.Model;

namespace PlaylistImport.Tests;

public class PlaylistImporterTests
{
    private readonly PlaylistImporter _importer = new();

    [Theory]
    [InlineData("set.M3U8", PlaylistFormat.M3u)]
    [InlineData("set.m3u", PlaylistFormat.M3u)]
    [InlineData("export.csv", PlaylistFormat.Csv)]
    [InlineData("export.tsv", PlaylistFormat.Csv)]
    [InlineData("rekordbox.xml", PlaylistFormat.RekordboxXml)]
    public void Picks_importer_by_extension(string file, PlaylistFormat expected) =>
        Assert.Equal(expected, _importer.ForFile(file)?.Format);

    [Fact]
    public void Returns_null_for_unknown_extension() => Assert.Null(_importer.ForFile("x.pls"));

    [Theory]
    [InlineData("rekordbox.xml", 2)]
    [InlineData("set.m3u8", 1)]
    [InlineData("export.csv", 1)]
    public async Task Imports_files_from_disk(string file, int playlists)
    {
        var result = await _importer.ImportFileAsync(Sample.Path(file), TestContext.Current.CancellationToken);
        Assert.Equal(playlists, result.Count);
    }

    [Fact]
    public async Task Throws_for_unknown_extension() =>
        await Assert.ThrowsAsync<NotSupportedException>(() => _importer.ImportFileAsync("x.pls", TestContext.Current.CancellationToken));
}
