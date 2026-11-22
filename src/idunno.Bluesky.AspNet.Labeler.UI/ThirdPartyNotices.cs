// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Html;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>
/// Reads the repository's THIRD-PARTY-NOTICES.txt, embedded into this assembly at build time, and parses
/// its plain-text layout into sections, paragraphs and tables for display on the acknowledgements page.
/// </summary>
/// <remarks>
/// <para>A section heading is a non-indented line followed by a line made only of hyphens. A table is a block whose second
/// line contains hyphen runs separated by at least two spaces; those runs define the column positions. Sections without a
/// table whose titles mention a license hold verbatim license text and are kept preformatted.</para>
/// </remarks>
internal static partial class ThirdPartyNotices
{
    /// <summary>The manifest resource name of the embedded notices file.</summary>
    internal const string ResourceName = "Xenon.THIRD-PARTY-NOTICES.txt";

    /// <summary>The file name offered when the notices are downloaded.</summary>
    internal const string FileName = "THIRD-PARTY-NOTICES.txt";

    private static readonly Lazy<string> s_text = new(ReadEmbeddedText);

    private static readonly Lazy<NoticeDocument> s_document = new(() => Parse(s_text.Value));

    /// <summary>Gets the embedded notices text, or an empty string when the resource is missing.</summary>
    internal static string Text => s_text.Value;

    /// <summary>Gets the parsed embedded notices.</summary>
    internal static NoticeDocument Document => s_document.Value;

    /// <summary>Parses plain-text notices into an introduction and titled sections.</summary>
    /// <param name="text">The notices text.</param>
    /// <returns>The parsed notices.</returns>
    internal static NoticeDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string[] lines = text.ReplaceLineEndings("\n").Split('\n');
        List<INoticeBlock> introduction = [];
        List<NoticeSection> sections = [];
        string? title = null;
        List<string> body = [];

        int i = 0;
        while (i < lines.Length)
        {
            if (i + 1 < lines.Length && IsHeading(lines[i], lines[i + 1]))
            {
                Flush(title, body, introduction, sections);
                title = lines[i].Trim();
                body = [];
                i += 2;
                continue;
            }

            body.Add(lines[i]);
            i++;
        }

        Flush(title, body, introduction, sections);

        return new NoticeDocument(introduction, sections);
    }

    /// <summary>HTML-encodes text and turns absolute HTTP(S) URLs into links.</summary>
    /// <param name="text">The text to render.</param>
    /// <returns>The encoded content.</returns>
    internal static IHtmlContent Linkify(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        HtmlContentBuilder builder = new();
        int position = 0;
        foreach (Match match in UrlPattern().Matches(text))
        {
            builder.Append(text[position..match.Index]);
            string encoded = HtmlEncoder.Default.Encode(match.Value);
            builder.AppendHtml($"<a href=\"{encoded}\" rel=\"noopener noreferrer\">{encoded}</a>");
            position = match.Index + match.Length;
        }

        builder.Append(text[position..]);

        return builder;
    }

    /// <summary>Reads the embedded notices so missing packaging shows an empty section rather than failing the page.</summary>
    private static string ReadEmbeddedText()
    {
        using Stream? stream = typeof(ThirdPartyNotices).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return string.Empty;
        }

        using StreamReader reader = new(stream);

        return reader.ReadToEnd();
    }

    /// <summary>Determines whether a line and the following underline form a section heading.</summary>
    private static bool IsHeading(string line, string underline) =>
        line.Length > 0 &&
        !char.IsWhiteSpace(line[0]) &&
        underline.TrimEnd() is { Length: >= 3 } trimmed &&
        trimmed.All(character => character == '-');

    /// <summary>Converts the collected body lines into blocks and appends them to the introduction or a new section.</summary>
    private static void Flush(
        string? title,
        List<string> body,
        List<INoticeBlock> introduction,
        List<NoticeSection> sections)
    {
        List<List<string>> groups = SplitGroups(body);
        if (title is null)
        {
            introduction.AddRange(groups.Select(ToBlock));
            return;
        }

        bool hasTable = groups.Exists(IsTable);
        if (!hasTable && title.Contains("license", StringComparison.OrdinalIgnoreCase))
        {
            string licenseText = string.Join('\n', TrimBlankLines(body));
            sections.Add(new NoticeSection(title, [], licenseText));
            return;
        }

        sections.Add(new NoticeSection(title, [.. groups.Select(ToBlock)], null));
    }

    /// <summary>Splits lines into groups separated by blank lines.</summary>
    private static List<List<string>> SplitGroups(List<string> lines)
    {
        List<List<string>> groups = [];
        List<string> current = [];
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (current.Count > 0)
                {
                    groups.Add(current);
                    current = [];
                }

                continue;
            }

            current.Add(line.TrimEnd());
        }

        if (current.Count > 0)
        {
            groups.Add(current);
        }

        return groups;
    }

    /// <summary>Removes leading and trailing blank lines while preserving interior spacing and indentation.</summary>
    private static IEnumerable<string> TrimBlankLines(List<string> lines)
    {
        int start = lines.FindIndex(line => !string.IsNullOrWhiteSpace(line));
        if (start < 0)
        {
            return [];
        }

        int end = lines.FindLastIndex(line => !string.IsNullOrWhiteSpace(line));

        return lines.GetRange(start, end - start + 1).Select(line => line.TrimEnd());
    }

    /// <summary>Determines whether a group is a column-aligned table with a hyphenated separator row.</summary>
    private static bool IsTable(List<string> group) => group.Count >= 2 && TableSeparator().IsMatch(group[1]);

    /// <summary>Converts a group of lines into a table, preformatted block or wrapped paragraph.</summary>
    private static INoticeBlock ToBlock(List<string> group)
    {
        if (IsTable(group))
        {
            List<int> starts = [.. TableColumn().Matches(group[1]).Select(match => match.Index)];
            return new NoticeTable(
                SplitColumns(group[0], starts),
                [.. group.Skip(2).Select(row => SplitColumns(row, starts))]);
        }

        if (group.Exists(line => char.IsWhiteSpace(line[0])))
        {
            return new NoticePreformatted(string.Join('\n', group));
        }

        return new NoticeParagraph(string.Join(' ', group.Select(line => line.Trim())));
    }

    /// <summary>Splits a fixed-width row at the separator's column start positions.</summary>
    private static List<string> SplitColumns(string row, List<int> starts)
    {
        List<string> cells = [];
        for (int column = 0; column < starts.Count; column++)
        {
            int start = Math.Min(starts[column], row.Length);
            int end = column + 1 < starts.Count ? Math.Min(starts[column + 1], row.Length) : row.Length;
            cells.Add(row[start..end].Trim());
        }

        return cells;
    }

    [GeneratedRegex(@"^-+( {2,}-+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex TableSeparator();

    [GeneratedRegex("-+", RegexOptions.CultureInvariant)]
    private static partial Regex TableColumn();

    [GeneratedRegex(@"https?://[^\s<>""]+?(?=[.,;:)]*(?:\s|$))", RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();
}

/// <summary>Parsed third-party notices.</summary>
/// <param name="Introduction">The blocks that precede the first section heading.</param>
/// <param name="Sections">The titled sections, in file order.</param>
internal sealed record NoticeDocument(IReadOnlyList<INoticeBlock> Introduction, IReadOnlyList<NoticeSection> Sections);

/// <summary>A titled section of the notices.</summary>
/// <param name="Title">The section heading.</param>
/// <param name="Blocks">The section's paragraphs, tables and preformatted blocks; empty for verbatim license text.</param>
/// <param name="LicenseText">The verbatim license text when the section reproduces a license; otherwise <see langword="null"/>.</param>
internal sealed record NoticeSection(string Title, IReadOnlyList<INoticeBlock> Blocks, string? LicenseText);

/// <summary>A block of content within the notices.</summary>
internal interface INoticeBlock;

/// <summary>A paragraph whose hard-wrapped lines have been joined.</summary>
/// <param name="Text">The paragraph text.</param>
internal sealed record NoticeParagraph(string Text) : INoticeBlock;

/// <summary>A block whose indentation is significant and must be displayed verbatim.</summary>
/// <param name="Text">The block text.</param>
internal sealed record NoticePreformatted(string Text) : INoticeBlock;

/// <summary>A column-aligned table.</summary>
/// <param name="Headers">The column headings.</param>
/// <param name="Rows">The table rows, each with one cell per column.</param>
internal sealed record NoticeTable(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) : INoticeBlock;
