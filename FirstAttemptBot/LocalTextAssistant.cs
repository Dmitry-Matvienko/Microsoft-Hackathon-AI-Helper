using System.Text;
using System.Text.RegularExpressions;

namespace FirstAttemptBot;

public sealed class LocalTextAssistant : ITextAssistant
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","and","for","that","this","with","from","your","you","are","was","were","have","has","had",
        "not","but","can","will","would","could","should","into","about","there","their","them","they",
        "what","when","where","which","who","whom","why","how","a","an","to","of","in","on","at","by",
        "is","it","as","be","or","if","we","our","i","me","my","he","she","his","her","its","do","does",
        "did","done","than","then","also","more","most","less","least","very","just","only","may","might",
        "может","это","как","что","для","или","если","когда","где","почему","зачем","кто","чтобы","уже",
        "ещё","еще","вы","мы","они","она","оно","он","на","в","с","по","от","до","из","и","а","но"
    };

    public Task<string> SummarizeAsync(string text, CancellationToken cancellationToken)
    {
        var normalized = Normalize(text);
        if (string.IsNullOrWhiteSpace(normalized))
            return Task.FromResult("I need some text to summarize.");

        var sentences = SplitSentences(normalized);
        if (sentences.Count == 0)
            return Task.FromResult(BuildFallbackSummary(normalized));

        var keywords = ExtractTopKeywords(normalized, 6);
        var selected = SelectBestSentences(sentences, normalized, 3);

        var sb = new StringBuilder();
        sb.AppendLine("Summary");
        foreach (var sentence in selected)
        {
            sb.AppendLine($"- {TrimSentence(sentence)}");
        }

        if (keywords.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Key topics");
            sb.AppendLine($"- {string.Join(", ", keywords)}");
        }

        sb.AppendLine();
        sb.AppendLine("Next steps");
        sb.AppendLine("- Review the main idea.");
        sb.AppendLine("- Check any important names, dates, or numbers.");
        sb.AppendLine("- Turn this into a short action list.");

        return Task.FromResult(sb.ToString().Trim());
    }

    public Task<string> CreateQuizAsync(string text, CancellationToken cancellationToken)
    {
        var normalized = Normalize(text);
        var keywords = ExtractTopKeywords(normalized, 4);

        if (keywords.Count == 0)
            keywords = new List<string> { "the topic", "the main idea", "the details" };

        var sb = new StringBuilder();
        sb.AppendLine("Mini quiz");
        sb.AppendLine($"1. What is the main point of {keywords[0]}?");
        sb.AppendLine($"2. Which detail about {keywords[Math.Min(1, keywords.Count - 1)]} is most important?");
        sb.AppendLine($"3. How would you explain {keywords[Math.Min(2, keywords.Count - 1)]} in one sentence?");
        sb.AppendLine();
        sb.AppendLine("Answer hint");
        sb.AppendLine("- Use the source text and keep the answers short.");

        return Task.FromResult(sb.ToString().Trim());
    }

    public async Task<string> BuildDefaultResponseAsync(string text, CancellationToken cancellationToken)
    {
        var summary = await SummarizeAsync(text, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("I read your message.");
        sb.AppendLine();
        sb.AppendLine(summary);
        return sb.ToString().Trim();
    }

    private static string Normalize(string text)
        => Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();

    private static string BuildFallbackSummary(string text)
    {
        var snippet = text.Length <= 300 ? text : text[..300] + "...";
        return $"Summary\n- {snippet}\n\nNext steps\n- Review the text.\n- Extract the main action items.";
    }

    private static List<string> SplitSentences(string text)
    {
        var parts = Regex.Split(text, @"(?<=[\.\!\?])\s+")
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(text))
            parts.Add(text);

        return parts;
    }

    private static List<string> SelectBestSentences(List<string> sentences, string wholeText, int maxCount)
    {
        var frequencies = BuildWordFrequency(wholeText);
        var scored = sentences
            .Select((sentence, index) => new
            {
                Index = index,
                Sentence = sentence,
                Score = ScoreSentence(sentence, frequencies)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Index)
            .Take(maxCount)
            .OrderBy(x => x.Index)
            .Select(x => x.Sentence)
            .ToList();

        if (scored.Count == 0)
            scored.Add(sentences[0]);

        return scored;
    }

    private static Dictionary<string, int> BuildWordFrequency(string text)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(text.ToLowerInvariant(), @"\p{L}{2,}"))
        {
            var word = match.Value;
            if (StopWords.Contains(word))
                continue;

            dict[word] = dict.TryGetValue(word, out var count) ? count + 1 : 1;
        }
        return dict;
    }

    private static double ScoreSentence(string sentence, IReadOnlyDictionary<string, int> frequencies)
    {
        var words = Regex.Matches(sentence.ToLowerInvariant(), @"\p{L}{2,}")
            .Select(m => m.Value)
            .Where(w => !StopWords.Contains(w))
            .ToList();

        if (words.Count == 0)
            return 0;

        var score = words.Sum(w => frequencies.TryGetValue(w, out var count) ? count : 0);
        return score / Math.Sqrt(words.Count);
    }

    private static List<string> ExtractTopKeywords(string text, int maxCount)
    {
        var frequencies = BuildWordFrequency(text);
        return frequencies
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key)
            .Take(maxCount)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private static string TrimSentence(string sentence, int maxLength = 220)
        => sentence.Length <= maxLength ? sentence : sentence[..maxLength].TrimEnd() + "...";
}
