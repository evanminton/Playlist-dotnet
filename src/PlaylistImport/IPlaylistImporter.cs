using PlaylistImport.Model;

namespace PlaylistImport;

/// <summary>Reads playlists of one format.</summary>
public interface IPlaylistImporter
{
    /// <summary>The format this importer reads.</summary>
    PlaylistFormat Format { get; }

    /// <summary>File extensions (with leading dot, lower case) this importer claims.</summary>
    IReadOnlyCollection<string> Extensions { get; }

    /// <summary>
    /// Reads every playlist in <paramref name="stream"/>. Single-playlist formats return one item;
    /// Rekordbox XML returns one per playlist node.
    /// </summary>
    /// <param name="stream">Readable stream positioned at the start of the file.</param>
    /// <param name="sourceName">File name, used as the playlist name when the file does not carry one.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="PlaylistImportException">The content is not valid for this format.</exception>
    Task<IReadOnlyList<Playlist>> ImportAsync(Stream stream, string? sourceName = null, CancellationToken cancellationToken = default);
}

/// <summary>Thrown when a file cannot be parsed as the requested format.</summary>
public sealed class PlaylistImportException(string message, Exception? inner = null) : Exception(message, inner);
