namespace PlaylistImport.Tests;

internal static class Sample
{
    public static string Path(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "Samples", name);

    public static Stream Open(string name) => File.OpenRead(Path(name));

    public static Stream FromText(string text) => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
}
