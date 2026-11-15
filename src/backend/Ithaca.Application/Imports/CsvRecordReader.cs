using System.Text;

namespace Ithaca.Application.Imports;

/// <summary>
/// RFC 4180 records from text, as Letterboxd writes them: comma-separated, fields optionally in double quotes, <c>""</c>
/// for a quote inside one, commas and line breaks allowed inside quotes, CRLF or LF line ends. Blank lines are skipped.
/// </summary>
public sealed class CsvRecordReader(TextReader reader)
{
    /// <summary>The next record's fields, or null at the end.</summary>
    public List<string>? Next()
    {
        while (true)
        {
            var record = ReadRecord();
            if (record is null)
            {
                return null;
            }

            if (record is not [""])
            {
                return record;
            }
        }
    }

    private List<string>? ReadRecord()
    {
        var c = reader.Read();
        if (c < 0)
        {
            return null;
        }

        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        while (true)
        {
            if (quoted)
            {
                if (c < 0)
                {
                    fields.Add(field.ToString()); // unterminated quote at the end: keep what we have
                    return fields;
                }

                if (c == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append((char)c);
                }
            }
            else if (c < 0 || c == '\n' || c == '\r')
            {
                if (c == '\r' && reader.Peek() == '\n')
                {
                    reader.Read();
                }

                fields.Add(field.ToString());
                return fields;
            }
            else if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (c == '"' && field.Length == 0)
            {
                quoted = true;
            }
            else
            {
                field.Append((char)c);
            }

            c = reader.Read();
        }
    }
}
