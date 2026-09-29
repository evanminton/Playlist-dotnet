using PlaylistImport.Formats.Csv;
using PlaylistImport.Formats.M3u;
using PlaylistImport.Formats.Rekordbox;
using PlaylistImport.Model;

namespace PlaylistImport;

/// <summary>
/// Entry point that picks the right <see cref="IPlaylistExporter"/> for a file by its extension.
/// Construct with the default set, or pass your own exporters to add formats.
/// </summary>
public sealed class PlaylistExporter
{
    private readonly IReadOnlyList<IPlaylistExporter> _exporters;

    /// <summary>Creates an exporter that writes Rekordbox XML, CSV and M3U/M3U8.</summary>
    public PlaylistExporter()
        : this([new RekordboxXmlExporter(), new CsvPlaylistExporter(), new M3uPlaylistExporter()])
    {
    }

    /// <summary>Creates an exporter over a custom set of format exporters.</summary>
    public PlaylistExporter(IEnumerable<IPlaylistExporter> exporters)
    {
        ArgumentNullException.ThrowIfNull(exporters);
        _exporters = [.. exporters];
    }

    /// <summary>The exporter registered for <paramref name="format"/>.</summary>
    /// <exception cref="NotSupportedException">No exporter is registered for the format.</exception>
    public IPlaylistExporter For(PlaylistFormat format) =>
        _exporters.FirstOrDefault(e => e.Format == format)
        ?? throw new NotSupportedException($"No exporter registered for {format}.");

    /// <summary>Finds the exporter that claims <paramref name="fileName"/>'s extension, or null.</summary>
    public IPlaylistExporter? ForFile(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return _exporters.FirstOrDefault(e => e.Extensions.Contains(extension));
    }

    /// <summary>Writes playlists to a file, choosing the format by extension. Overwrites an existing file.</summary>
    /// <exception cref="NotSupportedException">The extension is not recognised.</exception>
    /// <exception cref="ArgumentException">The format holds one playlist and several were given.</exception>
    public async Task ExportFileAsync(string path, IReadOnlyList<Playlist> playlists, CancellationToken cancellationToken = default)
    {
        var exporter = ForFile(path)
            ?? throw new NotSupportedException($"Unrecognised playlist file extension: '{Path.GetExtension(path)}'.");

        await using var stream = File.Create(path);
        await exporter.ExportAsync(playlists, stream, cancellationToken);
    }

    /// <summary>Writes a single playlist to a file, choosing the format by extension.</summary>
    public Task ExportFileAsync(string path, Playlist playlist, CancellationToken cancellationToken = default) =>
        ExportFileAsync(path, [playlist], cancellationToken);

    /// <summary>Writes playlists to a stream in a known format.</summary>
    public Task ExportAsync(IReadOnlyList<Playlist> playlists, Stream stream, PlaylistFormat format, CancellationToken cancellationToken = default) =>
        For(format).ExportAsync(playlists, stream, cancellationToken);
}
