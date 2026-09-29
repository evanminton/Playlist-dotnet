using PlaylistImport.Model;

namespace PlaylistImport;

internal static class ExportGuard
{
    /// <summary>Returns the only playlist, for formats that hold exactly one per file.</summary>
    public static Playlist Single(IReadOnlyList<Playlist> playlists, PlaylistFormat format)
    {
        ArgumentNullException.ThrowIfNull(playlists);
        return playlists.Count == 1
            ? playlists[0]
            : throw new ArgumentException($"{format} holds exactly one playlist per file, but {playlists.Count} were given.", nameof(playlists));
    }
}
