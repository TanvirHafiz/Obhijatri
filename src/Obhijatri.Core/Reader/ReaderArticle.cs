using System.Text;
using System.Text.Json;

namespace Obhijatri.Core.Reader;

public enum ReaderBlockKind
{
    Heading,
    Paragraph,
    ListItem,
    Quote,
}

public sealed record ReaderBlock(ReaderBlockKind Kind, string Text);

/// <summary>An article pulled out of a web page for reader mode: text only, no images, scripts or styles.</summary>
public sealed record ReaderArticle(string Title, string Site, IReadOnlyList<ReaderBlock> Blocks)
{
    public const int MaxBlocks = 1500;
    public const int MaxBlockLength = 4000;
    public const int MaxTitleLength = 300;
    public const int MaxTotalLength = 200_000;

    /// <summary>Fewer paragraphs than this and the page is not an article (a home page, a login form).</summary>
    public const int MinParagraphs = 2;

    /// <summary>
    /// Reads the extraction script's result. The page's own scripts could have tampered with it, so
    /// everything is treated as untrusted: unknown kinds are dropped, control characters are removed,
    /// and sizes are capped. Returns null when the page has too little text to be an article.
    /// </summary>
    public static ReaderArticle? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("blocks", out var array) || array.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var blocks = new List<ReaderBlock>();
            var total = 0;
            foreach (var item in array.EnumerateArray())
            {
                if (blocks.Count >= MaxBlocks || total >= MaxTotalLength)
                {
                    break;
                }
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("t", out var kindElement) || kindElement.ValueKind != JsonValueKind.String
                    || !item.TryGetProperty("x", out var textElement) || textElement.ValueKind != JsonValueKind.String
                    || ToKind(kindElement.GetString()) is not { } kind)
                {
                    continue;
                }

                var text = Clean(textElement.GetString(), MaxBlockLength);
                if (text.Length == 0)
                {
                    continue;
                }
                blocks.Add(new ReaderBlock(kind, text));
                total += text.Length;
            }

            if (blocks.Count(b => b.Kind == ReaderBlockKind.Paragraph) < MinParagraphs)
            {
                return null;
            }

            var title = Clean(ReadString(root, "title"), MaxTitleLength);
            return new ReaderArticle(title, Clean(ReadString(root, "site"), 100), blocks);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static ReaderBlockKind? ToKind(string? code) => code switch
    {
        "h" => ReaderBlockKind.Heading,
        "p" => ReaderBlockKind.Paragraph,
        "li" => ReaderBlockKind.ListItem,
        "q" => ReaderBlockKind.Quote,
        _ => null,
    };

    /// <summary>Collapses whitespace, drops control and invisible formatting characters, and cuts to a length.</summary>
    internal static string Clean(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var result = new StringBuilder(Math.Min(text.Length, maxLength));
        var lastWasSpace = true;
        foreach (var c in text)
        {
            if (result.Length >= maxLength)
            {
                break;
            }
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    result.Append(' ');
                    lastWasSpace = true;
                }
                continue;
            }
            // Keep the joiners that Bangla conjuncts need (U+200C, U+200D); drop other control and
            // bidirectional formatting characters that could reorder or hide text.
            if (char.IsControl(c) || c is >= '‪' and <= '‮' or >= '⁦' and <= '⁩' or '​' or '﻿')
            {
                continue;
            }
            result.Append(c);
            lastWasSpace = false;
        }
        return result.ToString().TrimEnd();
    }
}
