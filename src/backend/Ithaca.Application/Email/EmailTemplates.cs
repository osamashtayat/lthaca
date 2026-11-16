using System.Net;
using System.Reflection;

namespace Ithaca.Application.Email;

/// <summary>
/// Email copy lives in files, not C# (<c>Email/Templates/&lt;name&gt;.txt</c> and <c>.html</c>, embedded resources). The text
/// file's first line is <c>Subject: …</c>. Placeholders are <c>{{key}}</c>; values are HTML-encoded in the HTML body.
/// </summary>
public static class EmailTemplates
{
    private const string SubjectPrefix = "Subject: ";

    public static EmailMessage Render(string name, string to, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var text = Read($"{name}.txt").ReplaceLineEndings("\n");
        var newline = text.IndexOf('\n', StringComparison.Ordinal);
        if (!text.StartsWith(SubjectPrefix, StringComparison.Ordinal) || newline < 0)
        {
            throw new InvalidOperationException($"Email template '{name}.txt' must start with a '{SubjectPrefix}…' line.");
        }

        var subject = Fill(text[SubjectPrefix.Length..newline].Trim(), values, html: false);
        var body = Fill(text[(newline + 1)..].TrimStart('\n'), values, html: false);
        var html = Fill(Read($"{name}.html"), values, html: true);
        return new EmailMessage(to, subject, body, html);
    }

    private static string Fill(string template, IReadOnlyDictionary<string, string> values, bool html) =>
        values.Aggregate(template, (current, pair) =>
            current.Replace("{{" + pair.Key + "}}", html ? WebUtility.HtmlEncode(pair.Value) : pair.Value, StringComparison.Ordinal));

    private static string Read(string file)
    {
        var resource = $"{typeof(EmailTemplates).Namespace}.Templates.{file}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Email template '{file}' is missing (expected embedded resource {resource}).");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
