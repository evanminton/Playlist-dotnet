using PlaylistImport.Formats.Csv;
using PlaylistImport.Formats.M3u;
using PlaylistImport.Formats.Rekordbox;
using PlaylistImport.Model;

namespace PlaylistImport;

/// <summary>
/// Entry point that picks the right <see cref="IPlaylistImporter"/> for a file by its extension.
/// Construct with the default set, or pass your own importers to add formats.
/// </summary>
public sealed class PlaylistImporter
{
    private readonly IReadOnlyList<IPlaylistImporter> _importers;

    /// <summary>Creates an importer that handles Rekordbox XML, CSV/TSV and M3U/M3U8.</summary>
    public PlaylistImporter()
        : this([new RekordboxXmlImporter(), new CsvPlaylistImporter(), new M3uPlaylistImporter()])
    {
    }

    /// <summary>Creates an importer over a custom set of format importers.</summary>
    public PlaylistImporter(IEnumerable<IPlaylistImporter> importers)
    {
        ArgumentNullException.ThrowIfNull(importers);
        _importers = [.. importers];
    }

    /// <summary>The importer registered for <paramref name="format"/>.</summary>
    /// <exception cref="NotSupportedException">No importer is registered for the format.</exception>
    public IPlaylistImporter For(PlaylistFormat format) =>
        _importers.FirstOrDefault(i => i.Format == format)
        ?? throw new NotSupportedException($"No importer registered for {format}.");

    /// <summary>Finds the importer that claims <paramref name="fileName"/>'s extension, or null.</summary>
    public IPlaylistImporter? ForFile(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return _importers.FirstOrDefault(i => i.Extensions.Contains(extension));
    }

    /// <summary>Imports a file from disk, choosing the format by extension.</summary>
    /// <exception cref="NotSupportedException">The extension is not recognised.</exception>
    /// <exception cref="PlaylistImportException">The file content is invalid for its format.</exception>
    public async Task<IReadOnlyList<Playlist>> ImportFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var importer = ForFile(path)
            ?? throw new NotSupportedException($"Unrecognised playlist file extension: '{Path.GetExtension(path)}'.");

        await using var stream = File.OpenRead(path);
        return await importer.ImportAsync(stream, Path.GetFileName(path), cancellationToken);
    }

    /// <summary>Imports from a stream in a known format.</summary>
    public Task<IReadOnlyList<Playlist>> ImportAsync(Stream stream, PlaylistFormat format, string? sourceName = null, CancellationToken cancellationToken = default) =>
        For(format).ImportAsync(stream, sourceName, cancellationToken);
}
