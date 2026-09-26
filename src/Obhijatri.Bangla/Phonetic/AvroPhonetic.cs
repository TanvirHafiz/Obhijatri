/*
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at https://mozilla.org/MPL/2.0/.

    Software distributed under the License is distributed on an "AS IS"
    basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
    License for the specific language governing rights and limitations
    under the License.

    C# port of jsAvroPhonetic (avrolib.js from ibus-avro, commit dd521a1).
    The Original Code is jsAvroPhonetic.
    The Initial Developer of the Original Code is Rifat Nabi <to.rifat@gmail.com>.
    Copyright (C) OmicronLab (http://www.omicronlab.com). All Rights Reserved.
    Contributor(s): Obhijatri project (C# port).
*/

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Obhijatri.Bangla.Phonetic;

/// <summary>
/// Avro Phonetic: converts romanised Bangla ("ami banglay gan gai") to Bangla script
/// ("আমি বাংলায় গান গাই"). A faithful port of the original algorithm, including its quirks,
/// so that it gives exactly the same result as Avro Keyboard and as the injected web-page script.
/// </summary>
public sealed class AvroPhonetic
{
    private static readonly Lazy<AvroPhonetic> Shared = new(LoadEmbedded);

    private readonly Pattern[] _patterns;
    private readonly string _vowels;
    private readonly string _consonants;
    private readonly string _caseSensitive;

    private AvroPhonetic(RuleData data)
    {
        _patterns = data.Patterns.Select(Pattern.From).ToArray();
        _vowels = data.Vowel;
        _consonants = data.Consonant;
        _caseSensitive = data.CaseSensitive;
    }

    /// <summary>The engine with the bundled Avro rules.</summary>
    public static AvroPhonetic Instance => Shared.Value;

    /// <summary>The bundled rules as JSON, shared with the web-page script so both behave the same.</summary>
    public static string RulesJson => ReadResource("Obhijatri.Bangla.AvroPhoneticRules.json");

    public string Convert(string input)
    {
        var text = Fix(input);
        var output = new StringBuilder(text.Length * 2);

        for (var cur = 0; cur < text.Length; cur++)
        {
            var start = cur;
            var matched = false;

            foreach (var pattern in _patterns)
            {
                var end = cur + pattern.Find.Length;
                if (end > text.Length || string.CompareOrdinal(text, start, pattern.Find, 0, pattern.Find.Length) != 0)
                {
                    continue;
                }

                var prev = start - 1;
                foreach (var rule in pattern.Rules)
                {
                    if (rule.Matches.All(m => Satisfies(m, text, start, end, prev)))
                    {
                        output.Append(rule.Replace);
                        cur = end - 1;
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    output.Append(pattern.Replace);
                    cur = end - 1;
                    matched = true;
                }
                break;
            }

            if (!matched)
            {
                output.Append(text[cur]);
            }
        }

        return output.ToString();
    }

    /// <summary>True for characters the rules can turn into Bangla (used to decide what a "word" is while typing).</summary>
    public bool IsConvertible(char c) =>
        char.IsAsciiLetterOrDigit(c) || _patterns.Any(p => p.Find.Contains(c, StringComparison.Ordinal));

    private bool Satisfies(Match match, string text, int start, int end, int prev)
    {
        var check = match.IsSuffix ? end : prev;
        bool result;
        switch (match.Scope)
        {
            case "punctuation":
                result = (check < 0 && !match.IsSuffix)
                         || (check >= text.Length && match.IsSuffix)
                         || IsPunctuation(CharAt(text, check));
                break;
            case "vowel":
                result = ((check >= 0 && !match.IsSuffix) || (check < text.Length && match.IsSuffix))
                         && IsVowel(CharAt(text, check));
                break;
            case "consonant":
                result = ((check >= 0 && !match.IsSuffix) || (check < text.Length && match.IsSuffix))
                         && IsConsonant(CharAt(text, check));
                break;
            case "exact":
                int s, e;
                if (match.IsSuffix)
                {
                    s = end;
                    e = end + match.Value.Length;
                }
                else
                {
                    s = start - match.Value.Length;
                    e = start;
                }
                // The original compares "end < length" (not <=); kept for identical results.
                result = s >= 0 && e < text.Length && string.CompareOrdinal(text, s, match.Value, 0, match.Value.Length) == 0 && e - s == match.Value.Length;
                break;
            default:
                result = false;
                break;
        }
        return result ^ match.Negative;
    }

    // JavaScript's charAt returns "" outside the string, which is neither vowel nor consonant.
    private static char CharAt(string text, int index) => index >= 0 && index < text.Length ? text[index] : '\0';

    private bool IsVowel(char c) => c != '\0' && _vowels.Contains(char.ToLowerInvariant(c), StringComparison.Ordinal);

    private bool IsConsonant(char c) => c != '\0' && _consonants.Contains(char.ToLowerInvariant(c), StringComparison.Ordinal);

    private bool IsPunctuation(char c) => !(IsVowel(c) || IsConsonant(c));

    private string Fix(string input)
    {
        var chars = input.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!_caseSensitive.Contains(char.ToLowerInvariant(chars[i]), StringComparison.Ordinal))
            {
                chars[i] = char.ToLowerInvariant(chars[i]);
            }
        }
        return new string(chars);
    }

    private static AvroPhonetic LoadEmbedded()
    {
        var data = JsonSerializer.Deserialize(RulesJson, RuleJsonContext.Default.RuleData)
                   ?? throw new InvalidDataException("Avro rules are missing.");
        return new AvroPhonetic(data);
    }

    internal static string ReadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                           ?? throw new InvalidDataException("Missing embedded resource " + name);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private sealed record Match(bool IsSuffix, string Scope, bool Negative, string Value);

    private sealed record Rule(Match[] Matches, string Replace);

    private sealed record Pattern(string Find, string Replace, Rule[] Rules)
    {
        public static Pattern From(PatternData data) => new(
            data.Find,
            data.Replace,
            (data.Rules ?? []).Select(r => new Rule(
                r.Matches.Select(m =>
                {
                    var negative = m.Scope.StartsWith('!');
                    return new Match(m.Type == "suffix", negative ? m.Scope[1..] : m.Scope, negative, m.Value ?? string.Empty);
                }).ToArray(),
                r.Replace)).ToArray());
    }

    internal sealed class RuleData
    {
        [JsonPropertyName("patterns")] public PatternData[] Patterns { get; set; } = [];
        [JsonPropertyName("vowel")] public string Vowel { get; set; } = string.Empty;
        [JsonPropertyName("consonant")] public string Consonant { get; set; } = string.Empty;
        [JsonPropertyName("casesensitive")] public string CaseSensitive { get; set; } = string.Empty;
    }

    internal sealed class PatternData
    {
        [JsonPropertyName("find")] public string Find { get; set; } = string.Empty;
        [JsonPropertyName("replace")] public string Replace { get; set; } = string.Empty;
        [JsonPropertyName("rules")] public RuleDataItem[]? Rules { get; set; }
    }

    internal sealed class RuleDataItem
    {
        [JsonPropertyName("matches")] public MatchData[] Matches { get; set; } = [];
        [JsonPropertyName("replace")] public string Replace { get; set; } = string.Empty;
    }

    internal sealed class MatchData
    {
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("scope")] public string Scope { get; set; } = string.Empty;
        [JsonPropertyName("value")] public string? Value { get; set; }
    }
}

[JsonSerializable(typeof(AvroPhonetic.RuleData))]
internal sealed partial class RuleJsonContext : JsonSerializerContext;
