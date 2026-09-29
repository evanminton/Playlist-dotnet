using PlaylistImport.Model;

namespace PlaylistImport;

/// <summary>Writes playlists in one format.</summary>
public interface IPlaylistExporter
{
    /// <summary>The format this exporter writes.</summary>
    PlaylistFormat Format { get; }

    /// <summary>File extensions (with leading dot, lower case) this exporter claims; the first is the default.</summary>
    IReadOnlyCollection<string> Extensions { get; }

    /// <summary>Whether the format can hold more than one playlist per file (only Rekordbox XML can).</summary>
    bool SupportsMultiplePlaylists { get; }

    /// <summary>
    /// Writes <paramref name="playlists"/> to <paramref name="stream"/>. Formats that hold a single
    /// playlist throw <see cref="ArgumentException"/> when given more than one.
    /// </summary>
    Task ExportAsync(IReadOnlyList<Playlist> playlists, Stream stream, CancellationToken cancellationToken = default);
}
