using System.Globalization;
using System.Text;
using System.Xml;
using PlaylistImport.Model;

namespace PlaylistImport.Formats.Rekordbox;

/// <summary>
/// Writes a Rekordbox library XML that Rekordbox can import via "Imported Library". Tracks are
/// de-duplicated into <c>COLLECTION</c> by location (tracks without one each get their own entry),
/// playlists are nested under folders from <see cref="Playlist.FolderPath"/>, and entries reference
/// tracks by TrackID.
/// </summary>
public sealed class RekordboxXmlExporter : IPlaylistExporter
{
    public PlaylistFormat Format => PlaylistFormat.RekordboxXml;

    public IReadOnlyCollection<string> Extensions { get; } = [".xml"];

    public bool SupportsMultiplePlaylists => true;

    public async Task ExportAsync(IReadOnlyList<Playlist> playlists, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playlists);
        ArgumentNullException.ThrowIfNull(stream);

        var (collection, keys) = BuildCollection(playlists);
        var root = BuildFolderTree(playlists);

        var settings = new XmlWriterSettings { Async = true, Indent = true, Encoding = new UTF8Encoding(false) };
        await using var xml = XmlWriter.Create(stream, settings);

        await xml.WriteStartDocumentAsync();
        xml.WriteStartElement("DJ_PLAYLISTS");
        xml.WriteAttributeString("Version", "1.0.0");

        xml.WriteStartElement("PRODUCT");
        xml.WriteAttributeString("Name", "PlaylistImport");
        xml.WriteAttributeString("Version", typeof(RekordboxXmlExporter).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");
        xml.WriteAttributeString("Company", "");
        xml.WriteEndElement();

        xml.WriteStartElement("COLLECTION");
        xml.WriteAttributeString("Entries", Invariant(collection.Count));
        foreach (var (id, track) in collection)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCollectionTrack(xml, id, track);
        }
        xml.WriteEndElement();

        xml.WriteStartElement("PLAYLISTS");
        WriteFolder(xml, "ROOT", root, keys);
        xml.WriteEndElement();

        xml.WriteEndElement();
        await xml.WriteEndDocumentAsync();
        await xml.FlushAsync();
    }

    private static (List<(int Id, PlaylistTrack Track)> Collection, Dictionary<PlaylistTrack, int> Keys) BuildCollection(IReadOnlyList<Playlist> playlists)
    {
        var collection = new List<(int, PlaylistTrack)>();
        var keys = new Dictionary<PlaylistTrack, int>(ReferenceEqualityComparer.Instance);
        var byLocation = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var track in playlists.SelectMany(p => p.Tracks))
        {
            if (keys.ContainsKey(track))
                continue;

            if (track.Location is { Length: > 0 } location && byLocation.TryGetValue(location, out var existing))
            {
                keys[track] = existing;
                continue;
            }

            var id = collection.Count + 1;
            collection.Add((id, track));
            keys[track] = id;
            if (track.Location is { Length: > 0 } loc)
                byLocation[loc] = id;
        }

        return (collection, keys);
    }

    private static void WriteCollectionTrack(XmlWriter xml, int id, PlaylistTrack track)
    {
        xml.WriteStartElement("TRACK");
        xml.WriteAttributeString("TrackID", Invariant(id));
        xml.WriteAttributeString("Name", track.Title ?? "");
        xml.WriteAttributeString("Artist", track.Artist ?? "");
        xml.WriteAttributeString("Album", track.Album ?? "");
        xml.WriteAttributeString("Genre", track.Genre ?? "");
        if (track.Duration is { } d)
            xml.WriteAttributeString("TotalTime", Invariant((long)Math.Round(d.TotalSeconds)));
        xml.WriteAttributeString("AverageBpm", (track.Bpm ?? 0).ToString("0.00", CultureInfo.InvariantCulture));
        xml.WriteAttributeString("Tonality", track.Key ?? "");
        if (EncodeLocation(track.Location) is { } location)
            xml.WriteAttributeString("Location", location);
        xml.WriteEndElement();
    }

    private static void WriteFolder(XmlWriter xml, string name, Folder folder, Dictionary<PlaylistTrack, int> keys)
    {
        xml.WriteStartElement("NODE");
        xml.WriteAttributeString("Type", "0");
        xml.WriteAttributeString("Name", name);
        xml.WriteAttributeString("Count", Invariant(folder.Children.Count));

        foreach (var child in folder.Children)
        {
            if (child is FolderChild.SubFolder sub)
            {
                WriteFolder(xml, sub.Name, sub.Folder, keys);
                continue;
            }

            var playlist = ((FolderChild.List)child).Playlist;
            xml.WriteStartElement("NODE");
            xml.WriteAttributeString("Type", "1");
            xml.WriteAttributeString("Name", playlist.Name);
            xml.WriteAttributeString("KeyType", "0");
            xml.WriteAttributeString("Entries", Invariant(playlist.Tracks.Count));
            foreach (var track in playlist.Tracks)
            {
                xml.WriteStartElement("TRACK");
                xml.WriteAttributeString("Key", Invariant(keys[track]));
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    /// <summary>Groups playlists into a folder tree, keeping first-seen order of folders and playlists.</summary>
    private static Folder BuildFolderTree(IReadOnlyList<Playlist> playlists)
    {
        var root = new Folder();
        foreach (var playlist in playlists)
        {
            var folder = root;
            foreach (var segment in playlist.FolderPath)
            {
                var sub = folder.Children.OfType<FolderChild.SubFolder>().FirstOrDefault(f => f.Name == segment);
                if (sub is null)
                {
                    sub = new FolderChild.SubFolder(segment, new Folder());
                    folder.Children.Add(sub);
                }
                folder = sub.Folder;
            }
            folder.Children.Add(new FolderChild.List(playlist));
        }
        return root;
    }

    /// <summary>
    /// Inverse of <see cref="RekordboxXmlImporter.DecodeLocation"/>: <c>C:\Music\a b.mp3</c> becomes
    /// <c>file://localhost/C:/Music/a%20b.mp3</c> and <c>/Users/me/a.mp3</c> becomes
    /// <c>file://localhost/Users/me/a.mp3</c>. URLs with a scheme are written unchanged.
    /// </summary>
    internal static string? EncodeLocation(string? location)
    {
        if (string.IsNullOrEmpty(location))
            return null;
        if (location.Contains("://", StringComparison.Ordinal))
            return location;

        var segments = location.Replace('\\', '/').TrimStart('/').Split('/');
        var isWindowsDrive = segments[0].Length == 2 && char.IsAsciiLetter(segments[0][0]) && segments[0][1] == ':';
        var encoded = segments.Select((s, i) => i == 0 && isWindowsDrive ? s : Uri.EscapeDataString(s));
        return "file://localhost/" + string.Join('/', encoded);
    }

    private static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed class Folder
    {
        public List<FolderChild> Children { get; } = [];
    }

    private abstract record FolderChild
    {
        public sealed record SubFolder(string Name, Folder Folder) : FolderChild;
        public sealed record List(Playlist Playlist) : FolderChild;
    }
}
