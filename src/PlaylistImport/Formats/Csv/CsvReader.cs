using System.Text;

namespace PlaylistImport.Formats.Csv;

/// <summary>Minimal RFC 4180 reader: quoted fields, doubled quotes, and newlines inside quotes.</summary>
internal static class CsvReader
{
    public static IEnumerable<IReadOnlyList<string>> ReadRows(string text, char delimiter)
    {
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                }
                else
                {
                    field.Append(c);
                }
                i++;
                continue;
            }

            if (c == '"' && field.Length == 0)
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                row.Add(field.ToString());
                field.Clear();
                yield return row;
                row = [];
            }
            else
            {
                field.Append(c);
            }
            i++;
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            yield return row;
        }
    }
}
