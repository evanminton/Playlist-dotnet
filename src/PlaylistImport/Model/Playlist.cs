namespace PlaylistImport.Model;

/// <summary>A playlist read from an external source, normalized to a common shape.</summary>
public sealed record Playlist
{
    /// <summary>Display name of the playlist.</summary>
    public required string Name { get; init; }

    /// <summary>Folder hierarchy the playlist lives under in the source (Rekordbox only); empty at the root.</summary>
    public IReadOnlyList<string> FolderPath { get; init; } = [];

    /// <summary>Tracks in playlist order.</summary>
    public IReadOnlyList<PlaylistTrack> Tracks { get; init; } = [];

    /// <summary>Format the playlist was imported from.</summary>
    public required PlaylistFormat Source { get; init; }
}

/// <summary>A single entry in a <see cref="Playlist"/>. Every field except position is optional because sources vary widely.</summary>
public sealed record PlaylistTrack
{
    /// <summary>1-based position within the playlist.</summary>
    public required int Position { get; init; }

    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public string? Genre { get; init; }
    public TimeSpan? Duration { get; init; }
    public double? Bpm { get; init; }

    /// <summary>Musical key as written by the source (e.g. "Am", "8A").</summary>
    public string? Key { get; init; }

    /// <summary>File path or URL of the audio, decoded to a plain path where the source used a file:// URI.</summary>
    public string? Location { get; init; }

    /// <summary>Identifier of the track in the source system, when it has one (Rekordbox TrackID).</summary>
    public string? SourceId { get; init; }
}

/// <summary>Supported import formats.</summary>
public enum PlaylistFormat
{
    M3u,
    Csv,
    RekordboxXml,
}
