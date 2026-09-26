using System.Text;
using System.Text.RegularExpressions;

namespace Obhijatri.Safety.Filtering;

/// <summary>What kind of resource a request is for (the Adblock Plus "type" options).</summary>
[Flags]
public enum RequestType
{
    None = 0,
    Script = 1 << 0,
    Image = 1 << 1,
    Stylesheet = 1 << 2,
    XmlHttpRequest = 1 << 3,
    Subdocument = 1 << 4,
    Font = 1 << 5,
    Media = 1 << 6,
    WebSocket = 1 << 7,
    Ping = 1 << 8,
    Object = 1 << 9,
    Other = 1 << 10,
    All = (1 << 11) - 1,
}

/// <summary>
/// One network filter in the supported subset of Adblock Plus syntax:
/// <c>||domain^</c>, <c>|</c> anchors, <c>*</c> wildcards, the <c>^</c> separator, <c>@@</c> exceptions,
/// and the options third-party, domain=, important, match-case and the resource types.
/// Rules with other options (popup, redirect, csp, rewrite, ...) are not created at all,
/// because applying them without their special behaviour could break pages.
/// </summary>
public sealed partial class FilterRule
{
    private static readonly Dictionary<string, RequestType> TypeOptions = new(StringComparer.Ordinal)
    {
        ["script"] = RequestType.Script,
        ["image"] = RequestType.Image,
        ["stylesheet"] = RequestType.Stylesheet,
        ["xmlhttprequest"] = RequestType.XmlHttpRequest,
        ["subdocument"] = RequestType.Subdocument,
        ["font"] = RequestType.Font,
        ["media"] = RequestType.Media,
        ["websocket"] = RequestType.WebSocket,
        ["ping"] = RequestType.Ping,
        ["object"] = RequestType.Object,
        ["other"] = RequestType.Other,
    };

    // Options that only change how matching is reported, or only affect element hiding; safe to ignore.
    private static readonly HashSet<string> IgnoredOptions = new(StringComparer.Ordinal)
    {
        "generichide", "elemhide", "genericblock", "1p", "first-party",
    };

    private Regex? _regex;

    private FilterRule(string text, string pattern, bool isException)
    {
        Text = text;
        Pattern = pattern;
        IsException = isException;
    }

    /// <summary>The rule as written in the list.</summary>
    public string Text { get; }

    /// <summary>The URL pattern, lower-cased unless match-case is set.</summary>
    public string Pattern { get; private set; }

    public bool IsException { get; }
    public bool IsImportant { get; private set; }
    public bool MatchCase { get; private set; }

    /// <summary>True: only third-party requests. False: only first-party. Null: both.</summary>
    public bool? ThirdParty { get; private set; }

    public RequestType Types { get; private set; } = RequestType.All;

    /// <summary>An exception with $document: turns blocking off for whole pages on matching sites.</summary>
    public bool IsDocumentException { get; private set; }

    public IReadOnlyList<string> IncludeDomains { get; private set; } = [];
    public IReadOnlyList<string> ExcludeDomains { get; private set; } = [];

    /// <summary>
    /// For plain <c>||example.com^</c> rules without options: the domain. These are kept in a fast
    /// hash set instead of being matched as patterns.
    /// </summary>
    public string? PlainDomain { get; private set; }

    public bool HasOptions => ThirdParty is not null || Types != RequestType.All
        || IncludeDomains.Count > 0 || ExcludeDomains.Count > 0 || IsImportant || MatchCase;

    /// <summary>Parses one line. Returns null for comments, element hiding and unsupported rules.</summary>
    public static FilterRule? Parse(string line)
    {
        var text = line.Trim();
        if (text.Length == 0 || text[0] is '!' or '[' || text.Contains("##", StringComparison.Ordinal)
            || text.Contains("#@#", StringComparison.Ordinal) || text.Contains("#?#", StringComparison.Ordinal)
            || text.Contains("#$#", StringComparison.Ordinal))
        {
            return null;
        }

        var isException = text.StartsWith("@@", StringComparison.Ordinal);
        var body = isException ? text[2..] : text;

        // Regular-expression rules (/.../) are not supported in v1.
        if (body.StartsWith('/') && (body.EndsWith('/') || body.Contains("/$", StringComparison.Ordinal)))
        {
            return null;
        }

        var dollar = body.LastIndexOf('$');
        var pattern = dollar >= 0 ? body[..dollar] : body;
        var options = dollar >= 0 ? body[(dollar + 1)..] : string.Empty;
        if (pattern.Length == 0 || pattern is "*" or "|" or "||")
        {
            return null;
        }

        var rule = new FilterRule(text, pattern, isException);
        if (options.Length > 0 && !rule.ApplyOptions(options))
        {
            return null;
        }

        var normalized = rule.MatchCase ? pattern : pattern.ToLowerInvariant();
        rule.Pattern = normalized;
        if (!rule.HasOptions && !rule.IsDocumentException && PlainDomainRule().Match(normalized) is { Success: true } m)
        {
            rule.PlainDomain = m.Groups[1].Value;
        }
        return rule;
    }

    private bool ApplyOptions(string options)
    {
        RequestType include = RequestType.None, exclude = RequestType.None;
        foreach (var raw in options.Split(','))
        {
            var option = raw.Trim();
            var negated = option.StartsWith('~');
            var name = negated ? option[1..] : option;
            var equals = name.IndexOf('=');
            var value = equals >= 0 ? name[(equals + 1)..] : string.Empty;
            if (equals >= 0)
            {
                name = name[..equals];
            }

            switch (name)
            {
                case "third-party" or "3p":
                    ThirdParty = !negated;
                    break;
                case "domain" when !negated:
                    var domains = value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    IncludeDomains = domains.Where(d => !d.StartsWith('~')).Select(d => d.ToLowerInvariant()).ToArray();
                    ExcludeDomains = domains.Where(d => d.StartsWith('~')).Select(d => d[1..].ToLowerInvariant()).ToArray();
                    // Wildcard domain entries (example.*) are not supported: skip the rule.
                    if (IncludeDomains.Concat(ExcludeDomains).Any(d => d.EndsWith(".*", StringComparison.Ordinal)))
                    {
                        return false;
                    }
                    break;
                case "important":
                    IsImportant = true;
                    break;
                case "match-case":
                    MatchCase = true;
                    break;
                case "document" when IsException && !negated:
                    IsDocumentException = true;
                    break;
                default:
                    if (TypeOptions.TryGetValue(name, out var type))
                    {
                        if (negated)
                        {
                            exclude |= type;
                        }
                        else
                        {
                            include |= type;
                        }
                    }
                    else if (!IgnoredOptions.Contains(name))
                    {
                        // popup, document (blocking), redirect, csp, rewrite, removeparam, method, ...
                        return false;
                    }
                    break;
            }
        }

        Types = (include == RequestType.None ? RequestType.All : include) & ~exclude;
        return Types != RequestType.None || IsDocumentException;
    }

    /// <summary>Does the pattern match this URL? (Options are checked by the engine.)</summary>
    public bool MatchesUrl(string url)
    {
        // Patterns and URLs are both lower-cased unless match-case is set, so no case-insensitive matching is needed.
        _regex ??= new Regex(ToRegex(Pattern), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        try
        {
            return _regex.IsMatch(url);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>Checks third-party, domain= and type options for a request.</summary>
    public bool MatchesContext(RequestType type, bool isThirdParty, string pageHost)
    {
        if ((Types & type) == 0 || (ThirdParty is bool thirdParty && thirdParty != isThirdParty))
        {
            return false;
        }
        if (ExcludeDomains.Any(d => HostMatches(pageHost, d)))
        {
            return false;
        }
        return IncludeDomains.Count == 0 || IncludeDomains.Any(d => HostMatches(pageHost, d));
    }

    /// <summary>host equals domain or is a subdomain of it.</summary>
    public static bool HostMatches(string host, string domain) =>
        host.Length == domain.Length
            ? string.Equals(host, domain, StringComparison.OrdinalIgnoreCase)
            : host.Length > domain.Length && host[host.Length - domain.Length - 1] == '.'
              && host.EndsWith(domain, StringComparison.OrdinalIgnoreCase);

    /// <summary>Converts an Adblock Plus pattern into an equivalent regular expression.</summary>
    internal static string ToRegex(string pattern)
    {
        var p = pattern;
        var regex = new StringBuilder();
        if (p.StartsWith("||", StringComparison.Ordinal))
        {
            // Start of the host name or of any subdomain of it.
            regex.Append(@"^[a-z][a-z0-9+.\-]*://(?:[^/?#]*\.)?");
            p = p[2..];
        }
        else if (p.StartsWith('|'))
        {
            regex.Append('^');
            p = p[1..];
        }

        var anchoredEnd = p.EndsWith('|');
        if (anchoredEnd)
        {
            p = p[..^1];
        }

        foreach (var c in p)
        {
            switch (c)
            {
                case '*':
                    regex.Append(".*");
                    break;
                case '^':
                    // A separator: anything except a letter, digit or one of _ - . %, or the end.
                    regex.Append(@"(?:[^\w\-.%]|$)");
                    break;
                default:
                    regex.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        if (anchoredEnd)
        {
            regex.Append('$');
        }
        return regex.ToString();
    }

    [GeneratedRegex(@"^\|\|([a-z0-9][a-z0-9.\-]*[a-z0-9])\^$", RegexOptions.CultureInvariant)]
    private static partial Regex PlainDomainRule();
}
