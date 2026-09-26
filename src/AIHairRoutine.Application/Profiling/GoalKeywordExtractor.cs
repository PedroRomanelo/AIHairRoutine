using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Profiling;

/// <summary>
/// Extracts care priorities from the free-text main goal by (accent-insensitive) keyword, in the
/// order they appear. An empty result means the text is ambiguous for rules (the hybrid profiler
/// then asks JEV).
/// </summary>
public static class GoalKeywordExtractor
{
    // Specific phrases come first: once matched, their span is consumed so "reduzir volume"
    // counts as frizz control and not as a volume goal.
    private static readonly (string Term, HairPriority Priority)[] Terms =
    [
        ("reduzir volume", HairPriority.FrizzControl),
        ("diminuir volume", HairPriority.FrizzControl),
        ("menos volume", HairPriority.FrizzControl),
        ("controlar volume", HairPriority.FrizzControl),
        ("reduce volume", HairPriority.FrizzControl),
        ("less volume", HairPriority.FrizzControl),
        ("pontas duplas", HairPriority.Reconstruction),
        ("split end", HairPriority.Reconstruction),
        ("sem brilho", HairPriority.Shine),
        ("sem vida", HairPriority.Shine),
        ("hair loss", HairPriority.HairLossControl),

        ("queda", HairPriority.HairLossControl),
        ("caindo", HairPriority.HairLossControl),
        ("cair", HairPriority.HairLossControl),
        ("crescimento", HairPriority.HairLossControl),
        ("crescer", HairPriority.HairLossControl),
        ("calvic", HairPriority.HairLossControl),
        ("falhas", HairPriority.HairLossControl),
        ("shedding", HairPriority.HairLossControl),
        ("falling", HairPriority.HairLossControl),
        ("growth", HairPriority.HairLossControl),
        ("thinning", HairPriority.HairLossControl),

        ("brilho", HairPriority.Shine),
        ("opaco", HairPriority.Shine),
        ("shine", HairPriority.Shine),
        ("shiny", HairPriority.Shine),
        ("dull", HairPriority.Shine),

        ("frizz", HairPriority.FrizzControl),
        ("armado", HairPriority.FrizzControl),
        ("arrepiado", HairPriority.FrizzControl),
        ("rebelde", HairPriority.FrizzControl),

        ("volume", HairPriority.Volume),
        ("encorpa", HairPriority.Volume),
        ("murcho", HairPriority.Volume),

        ("ressec", HairPriority.Hydration),
        ("seco", HairPriority.Hydration),
        ("hidrat", HairPriority.Hydration),
        ("macio", HairPriority.Hydration),
        ("maciez", HairPriority.Hydration),
        ("dry", HairPriority.Hydration),
        ("hydrat", HairPriority.Hydration),
        ("moistur", HairPriority.Hydration),
        ("soft", HairPriority.Hydration),

        ("quebr", HairPriority.Reconstruction),
        ("elastic", HairPriority.Reconstruction),
        ("emborrach", HairPriority.Reconstruction),
        ("danific", HairPriority.Reconstruction),
        ("fragil", HairPriority.Reconstruction),
        ("fortalec", HairPriority.Reconstruction),
        ("reconstr", HairPriority.Reconstruction),
        ("poros", HairPriority.Reconstruction),
        ("break", HairPriority.Reconstruction),
        ("damage", HairPriority.Reconstruction),
        ("repair", HairPriority.Reconstruction),
        ("strength", HairPriority.Reconstruction),

        ("oleos", HairPriority.OilControl),
        ("gordur", HairPriority.OilControl),
        ("oily", HairPriority.OilControl),
        ("greasy", HairPriority.OilControl),

        ("nutri", HairPriority.Nutrition),
        ("cacho", HairPriority.Nutrition),
        ("defini", HairPriority.Nutrition),
        ("nourish", HairPriority.Nutrition),
        ("curl", HairPriority.Nutrition),
    ];

    public static IReadOnlyList<HairPriority> Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var normalized = TextNormalizer.Normalize(text);
        var consumed = new bool[normalized.Length];
        var hits = new List<(int Index, HairPriority Priority)>();

        foreach (var (term, priority) in Terms)
        {
            int start = 0;
            while ((start = normalized.IndexOf(term, start, StringComparison.Ordinal)) >= 0)
            {
                var span = consumed.AsSpan(start, term.Length);
                if (!span.Contains(true))
                {
                    hits.Add((start, priority));
                    span.Fill(true);
                }

                start += term.Length;
            }
        }

        return hits.OrderBy(h => h.Index).Select(h => h.Priority).Distinct().ToList();
    }
}
