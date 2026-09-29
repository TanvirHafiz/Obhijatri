using System.Text;

namespace Obhijatri.Core.Reader;

/// <summary>Cuts text into pieces short enough to speak one at a time, at sentence ends where possible.</summary>
public static class SpeechChunker
{
    public const int DefaultMaxLength = 400;

    public static IReadOnlyList<string> Split(string text, int maxLength = DefaultMaxLength)
    {
        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var sentence in Sentences(text))
        {
            if (current.Length > 0 && current.Length + sentence.Length + 1 > maxLength)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }

            var rest = sentence;
            while (rest.Length > maxLength)
            {
                // A very long sentence: cut at the last space before the limit.
                var cut = rest.LastIndexOf(' ', maxLength - 1);
                if (cut <= 0)
                {
                    cut = maxLength;
                }
                if (current.Length > 0)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                }
                chunks.Add(rest[..cut].Trim());
                rest = rest[cut..].TrimStart();
            }

            if (rest.Length > 0)
            {
                if (current.Length > 0)
                {
                    current.Append(' ');
                }
                current.Append(rest);
            }
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
        }
        return chunks.Where(c => c.Length > 0).ToList();
    }

    /// <summary>Sentences end at a danda, full stop, question or exclamation mark followed by a space or the end.</summary>
    private static IEnumerable<string> Sentences(string text)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '।' or '.' or '?' or '!' && (i + 1 == text.Length || text[i + 1] == ' '))
            {
                var sentence = text[start..(i + 1)].Trim();
                if (sentence.Length > 0)
                {
                    yield return sentence;
                }
                start = i + 1;
            }
        }

        var tail = text[start..].Trim();
        if (tail.Length > 0)
        {
            yield return tail;
        }
    }
}
