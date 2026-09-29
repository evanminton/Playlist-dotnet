using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using PlaylistImport.Model;

namespace PlaylistImport.Formats.Rekordbox;

/// <summary>
/// Reads a Rekordbox library export ("File &gt; Export Collection in xml format"). Every playlist node
/// under <c>PLAYLISTS</c> becomes a <see cref="Playlist"/>, with its parent folders in
/// <see cref="Playlist.FolderPath"/>. Playlist entries are resolved against <c>COLLECTION</c> by
/// TrackID (KeyType 0) or by Location (KeyType 1).
/// </summary>
public sealed class RekordboxXmlImporter : IPlaylistImporter
{
    private const string FolderNodeType = "0";
    private const string KeyTypeLocation = "1";

    public PlaylistFormat Format => PlaylistFormat.RekordboxXml;

    public IReadOnlyCollection<string> Extensions { get; } = [".xml"];

    public async Task<IReadOnlyList<Playlist>> ImportAsync(Stream stream, string? sourceName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        XDocument doc;
        try
        {
            // DTDs are refused so an untrusted export cannot trigger entity expansion.
            var settings = new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var xml = XmlReader.Create(stream, settings);
            doc = await XDocument.LoadAsync(xml, LoadOptions.None, cancellationToken);
        }
        catch (XmlException ex)
        {
            throw new PlaylistImportException("File is not well-formed XML.", ex);
        }

        var root = doc.Root;
        if (root is null || root.Name.LocalName != "DJ_PLAYLISTS")
            throw new PlaylistImportException("Not a Rekordbox export: root element is not DJ_PLAYLISTS.");

        var collection = root.Element("COLLECTION")?.Elements("TRACK").ToList() ?? [];
        var byId = new Dictionary<string, XElement>(StringComparer.Ordinal);
        var byLocation = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var track in collection)
        {
            if ((string?)track.Attribute("TrackID") is { } id)
                byId.TryAdd(id, track);
            if ((string?)track.Attribute("Location") is { } location)
                byLocation.TryAdd(location, track);
        }

        var playlists = new List<Playlist>();
        var top = root.Element("PLAYLISTS")?.Element("NODE");
        if (top is not null)
        {
            // The top node is Rekordbox's implicit "ROOT" folder and is not part of the user's folder path.
            foreach (var child in top.Elements("NODE"))
                Walk(child, [], byId, byLocation, playlists);
        }

        return playlists;
    }

    private void Walk(XElement node, List<string> path, Dictionary<string, XElement> byId, Dictionary<string, XElement> byLocation, List<Playlist> output)
    {
        var name = (string?)node.Attribute("Name") ?? "Untitled";

        if ((string?)node.Attribute("Type") == FolderNodeType)
        {
            path.Add(name);
            foreach (var child in node.Elements("NODE"))
                Walk(child, path, byId, byLocation, output);
            path.RemoveAt(path.Count - 1);
            return;
        }

        var byPath = (string?)node.Attribute("KeyType") == KeyTypeLocation;
        var lookup = byPath ? byLocation : byId;
        var tracks = new List<PlaylistTrack>();
        foreach (var entry in node.Elements("TRACK"))
        {
            var key = (string?)entry.Attribute("Key");
            if (key is null)
                continue;

            tracks.Add(lookup.TryGetValue(key, out var track)
                ? ToTrack(track, tracks.Count + 1)
                // Dangling reference: keep the slot, and the path when the key is one.
                : new PlaylistTrack { Position = tracks.Count + 1, SourceId = key, Location = byPath ? DecodeLocation(key) : null });
        }

        output.Add(new Playlist
        {
            Name = name,
            FolderPath = [.. path],
            Tracks = tracks,
            Source = Format,
        });
    }

    private static PlaylistTrack ToTrack(XElement track, int position)
    {
        static string? Attr(XElement e, string name) =>
            (string?)e.Attribute(name) is { Length: > 0 } v ? v : null;

        static double? Number(XElement e, string name) =>
            double.TryParse(Attr(e, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;

        var seconds = Number(track, "TotalTime");
        var bpm = Number(track, "AverageBpm");

        return new PlaylistTrack
        {
            Position = position,
            SourceId = Attr(track, "TrackID"),
            Title = Attr(track, "Name"),
            Artist = Attr(track, "Artist"),
            Album = Attr(track, "Album"),
            Genre = Attr(track, "Genre"),
            Duration = seconds is { } s ? TimeSpan.FromSeconds(s) : null,
            Bpm = bpm is > 0 ? bpm : null,
            Key = Attr(track, "Tonality"),
            Location = DecodeLocation(Attr(track, "Location")),
        };
    }

    /// <summary>
    /// Turns Rekordbox's <c>file://localhost/...</c> URIs into plain paths:
    /// <c>file://localhost/C:/Music/a%20b.mp3</c> becomes <c>C:/Music/a b.mp3</c> and
    /// <c>file://localhost/Users/me/a.mp3</c> becomes <c>/Users/me/a.mp3</c>. Anything else is returned unchanged.
    /// </summary>
    internal static string? DecodeLocation(string? location)
    {
        const string prefix = "file://localhost/";
        if (location is null || !location.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return location;

        var path = Uri.UnescapeDataString(location[prefix.Length..]);
        var isWindowsDrive = path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';
        return isWindowsDrive ? path : "/" + path;
    }
}
