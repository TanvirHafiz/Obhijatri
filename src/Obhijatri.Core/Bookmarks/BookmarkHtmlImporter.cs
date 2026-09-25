using System.Net;
using System.Text.RegularExpressions;
using Obhijatri.Core.Storage;

namespace Obhijatri.Core.Bookmarks;

public sealed class ImportedBookmark
{
    public required string Title { get; init; }
    public string? Url { get; init; }
    public bool IsFolder => Url is null;
    public List<ImportedBookmark> Children { get; } = [];
}

public sealed record BookmarkImportResult(IReadOnlyList<ImportedBookmark> Items, int BookmarkCount, int SkippedCount);

/// <summary>
/// Reads the "Netscape bookmark file" HTML that Chrome, Edge and Firefox export.
/// The file is untrusted input: sizes are capped, and only http and https links are kept
/// (javascript: bookmarklets and file: links are skipped).
/// </summary>
public static partial class BookmarkHtmlImporter
{
    public const long MaxFileBytes = 20 * 1024 * 1024;
    private const int MaxDepth = 64;
    private const int MaxItems = 100_000;

    public static BookmarkImportResult Parse(string html)
    {
        var root = new List<ImportedBookmark>();
        var stack = new Stack<List<ImportedBookmark>>();
        stack.Push(root);
        List<ImportedBookmark>? pendingChildren = null;
        int bookmarks = 0, skipped = 0, items = 0;

        foreach (Match token in Tokens().Matches(html))
        {
            if (token.Groups["dlopen"].Success)
            {
                if (stack.Count >= MaxDepth)
                {
                    // Too deep: keep adding to the current level rather than nesting further.
                    stack.Push(stack.Peek());
                }
                else
                {
                    stack.Push(pendingChildren ?? stack.Peek());
                }
                pendingChildren = null;
            }
            else if (token.Groups["dlclose"].Success)
            {
                if (stack.Count > 1)
                {
                    stack.Pop();
                }
            }
            else if (token.Groups["h3"].Success)
            {
                if (++items > MaxItems)
                {
                    break;
                }

                if (ToolbarFolder().IsMatch(token.Groups["h3attrs"].Value))
                {
                    // Chrome "Bookmarks bar" / Edge "Favorites bar": put its items at the top level.
                    pendingChildren = stack.Peek();
                    continue;
                }

                var folder = new ImportedBookmark { Title = CleanText(token.Groups["h3text"].Value) };
                stack.Peek().Add(folder);
                pendingChildren = folder.Children;
            }
            else if (token.Groups["a"].Success)
            {
                if (++items > MaxItems)
                {
                    break;
                }

                var href = Href().Match(token.Groups["aattrs"].Value);
                var url = href.Success ? WebUtility.HtmlDecode(href.Groups[1].Value).Trim() : string.Empty;
                if (!BookmarkStore.IsAllowedUrl(url))
                {
                    skipped++;
                    continue;
                }

                var title = CleanText(token.Groups["atext"].Value);
                stack.Peek().Add(new ImportedBookmark { Title = title.Length > 0 ? title : url, Url = url });
                bookmarks++;
            }
        }

        return new BookmarkImportResult(root, bookmarks, skipped);
    }

    private static string CleanText(string raw)
    {
        var text = WebUtility.HtmlDecode(InnerTags().Replace(raw, string.Empty)).Trim();
        return text.Length <= BookmarkStore.MaxTitleLength ? text : text[..BookmarkStore.MaxTitleLength];
    }

    [GeneratedRegex(
        """(?<dlopen><DL\b)|(?<dlclose></DL\s*>)|(?<h3><H3\b(?<h3attrs>[^>]*)>(?<h3text>.*?)</H3\s*>)|(?<a><A\b(?<aattrs>[^>]*)>(?<atext>.*?)</A\s*>)""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex Tokens();

    [GeneratedRegex("""\bHREF\s*=\s*"([^"]*)" """, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Href();

    [GeneratedRegex("""\bPERSONAL_TOOLBAR_FOLDER\s*=\s*"true" """, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ToolbarFolder();

    [GeneratedRegex("<[^>]*>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InnerTags();
}
