namespace Obhijatri.Safety.Filtering;

public enum FilterAction
{
    Allow,
    Block,
}

public readonly record struct FilterDecision(FilterAction Action, string? Rule)
{
    public static readonly FilterDecision Allowed = new(FilterAction.Allow, null);
    public bool IsBlocked => Action == FilterAction.Block;
}

/// <summary>
/// Fast matching of requests against ad and tracker lists.
///  - Plain <c>||domain^</c> rules (about nine in ten) are kept as 64-bit hashes in a set, and a request
///    host is checked by looking up each of its parent domains.
///  - Other rules are indexed by one "token" (a word that must appear in any matching URL), so a
///    request only tests the few rules that share a word with its URL.
/// Immutable once built, so it can be used from any thread.
/// </summary>
public sealed class FilterEngine
{
    private static readonly HashSet<string> CommonTokens = new(StringComparer.Ordinal)
    {
        "http", "https", "www", "com", "net", "org", "js", "html", "htm", "php", "png", "jpg", "gif", "css", "api",
    };

    private readonly HashSet<ulong> _blockDomains = [];
    private readonly HashSet<ulong> _allowDomains = [];
    private readonly Dictionary<ulong, List<FilterRule>> _blockByToken = [];
    private readonly Dictionary<ulong, List<FilterRule>> _allowByToken = [];
    private readonly List<FilterRule> _blockUntokenized = [];
    private readonly List<FilterRule> _allowUntokenized = [];
    private readonly List<FilterRule> _important = [];
    private readonly List<FilterRule> _documentExceptions = [];

    private FilterEngine()
    {
    }

    public static FilterEngine Empty { get; } = new();

    public int RuleCount { get; private set; }
    public int DomainRuleCount => _blockDomains.Count + _allowDomains.Count;
    public int UntokenizedCount => _blockUntokenized.Count + _allowUntokenized.Count;

    public static FilterEngine Build(IEnumerable<string> lines)
    {
        var engine = new FilterEngine();
        foreach (var line in lines)
        {
            if (FilterRule.Parse(line) is { } rule)
            {
                engine.Add(rule);
            }
        }
        return engine;
    }

    private void Add(FilterRule rule)
    {
        RuleCount++;
        if (rule.IsDocumentException)
        {
            _documentExceptions.Add(rule);
            return;
        }
        if (rule.PlainDomain is { } domain)
        {
            (rule.IsException ? _allowDomains : _blockDomains).Add(Hash(domain));
            return;
        }
        if (rule.IsImportant && !rule.IsException)
        {
            _important.Add(rule);
            return;
        }

        var token = BestToken(rule.MatchCase ? rule.Pattern.ToLowerInvariant() : rule.Pattern);
        if (token is null)
        {
            (rule.IsException ? _allowUntokenized : _blockUntokenized).Add(rule);
            return;
        }

        var index = rule.IsException ? _allowByToken : _blockByToken;
        var key = Hash(token);
        if (!index.TryGetValue(key, out var bucket))
        {
            index[key] = bucket = [];
        }
        bucket.Add(rule);
    }

    /// <summary>Is filtering switched off for this whole page by a $document exception?</summary>
    public bool IsPageExcepted(string pageUrl)
    {
        if (_documentExceptions.Count == 0 || !Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }
        var lower = pageUrl.ToLowerInvariant();
        return _documentExceptions.Any(r => r.MatchesContext(RequestType.All, false, uri.Host) && r.MatchesUrl(r.MatchCase ? pageUrl : lower));
    }

    public FilterDecision Check(string url, string requestHost, string pageHost, RequestType type)
    {
        var host = requestHost.ToLowerInvariant();
        var page = pageHost.ToLowerInvariant();
        var lower = url.ToLowerInvariant();
        var thirdParty = RegistrableDomain.IsThirdParty(host, page);

        foreach (var rule in _important)
        {
            if (Matches(rule, url, lower, type, thirdParty, page))
            {
                return new FilterDecision(FilterAction.Block, rule.Text);
            }
        }

        string? blockedBy = null;
        if (AnyParentDomain(_blockDomains, host) is { } domain)
        {
            blockedBy = "||" + domain + "^";
        }
        else
        {
            blockedBy = FindRule(_blockByToken, _blockUntokenized, url, lower, type, thirdParty, page)?.Text;
        }

        if (blockedBy is null)
        {
            return FilterDecision.Allowed;
        }

        if (AnyParentDomain(_allowDomains, host) is not null
            || FindRule(_allowByToken, _allowUntokenized, url, lower, type, thirdParty, page) is not null)
        {
            return FilterDecision.Allowed;
        }

        return new FilterDecision(FilterAction.Block, blockedBy);
    }

    private static FilterRule? FindRule(Dictionary<ulong, List<FilterRule>> index, List<FilterRule> untokenized,
        string url, string lower, RequestType type, bool thirdParty, string page)
    {
        foreach (var token in Tokens(lower))
        {
            if (index.TryGetValue(token, out var bucket))
            {
                foreach (var rule in bucket)
                {
                    if (Matches(rule, url, lower, type, thirdParty, page))
                    {
                        return rule;
                    }
                }
            }
        }
        foreach (var rule in untokenized)
        {
            if (Matches(rule, url, lower, type, thirdParty, page))
            {
                return rule;
            }
        }
        return null;
    }

    private static bool Matches(FilterRule rule, string url, string lower, RequestType type, bool thirdParty, string page) =>
        rule.MatchesContext(type, thirdParty, page) && rule.MatchesUrl(rule.MatchCase ? url : lower);

    /// <summary>Returns the first of host, its parent domains, ... that is in the set.</summary>
    private static string? AnyParentDomain(HashSet<ulong> set, string host)
    {
        if (set.Count == 0)
        {
            return null;
        }
        var start = 0;
        while (start < host.Length)
        {
            if (set.Contains(Hash(host.AsSpan(start))))
            {
                return host[start..];
            }
            var dot = host.IndexOf('.', start);
            if (dot < 0)
            {
                break;
            }
            start = dot + 1;
        }
        return null;
    }

    private static bool IsTokenChar(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '%';

    /// <summary>Hashes of every word (run of letters, digits and %) in the URL.</summary>
    private static IEnumerable<ulong> Tokens(string lowerUrl)
    {
        var seen = new HashSet<ulong>();
        var i = 0;
        while (i < lowerUrl.Length)
        {
            if (!IsTokenChar(lowerUrl[i]))
            {
                i++;
                continue;
            }
            var start = i;
            while (i < lowerUrl.Length && IsTokenChar(lowerUrl[i]))
            {
                i++;
            }
            if (i - start >= 2)
            {
                var hash = Hash(lowerUrl.AsSpan(start, i - start));
                if (seen.Add(hash))
                {
                    yield return hash;
                }
            }
        }
    }

    /// <summary>
    /// The longest word in the pattern that must appear as a whole word in any matching URL.
    /// A word next to "*" or at an unanchored edge could be part of a longer word, so it is skipped.
    /// </summary>
    internal static string? BestToken(string pattern)
    {
        var body = pattern;
        var anchoredStart = false;
        if (body.StartsWith("||", StringComparison.Ordinal))
        {
            body = body[2..];
            anchoredStart = true;
        }
        else if (body.StartsWith('|'))
        {
            body = body[1..];
            anchoredStart = true;
        }
        var anchoredEnd = body.EndsWith('|');
        if (anchoredEnd)
        {
            body = body[..^1];
        }

        string? best = null;
        var i = 0;
        while (i < body.Length)
        {
            if (!IsTokenChar(body[i]))
            {
                i++;
                continue;
            }
            var start = i;
            while (i < body.Length && IsTokenChar(body[i]))
            {
                i++;
            }

            var beforeOk = start == 0 ? anchoredStart : body[start - 1] != '*';
            var afterOk = i == body.Length ? anchoredEnd : body[i] != '*';
            var token = body[start..i];
            if (beforeOk && afterOk && token.Length >= 2 && !CommonTokens.Contains(token)
                && (best is null || token.Length > best.Length))
            {
                best = token;
            }
        }
        return best;
    }

    /// <summary>FNV-1a, 64-bit.</summary>
    private static ulong Hash(ReadOnlySpan<char> text)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return hash;
    }
}
