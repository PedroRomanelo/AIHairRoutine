using System.Globalization;
using System.Text.RegularExpressions;

namespace AIHairRoutine.Application.Profiling;

/// <summary>
/// Resolves the "when was the chemical done" answer into days elapsed. Accepts relative text
/// ("há 2 meses", "3 semanas", "uma semana", "2 months ago") and dates ("10/06/2026", "2026-06-10",
/// "06/2026"). Future dates are rejected.
/// </summary>
public sealed partial class ChemicalTimingParser(TimeProvider time)
{
    private const int MaxDays = 100 * 365;

    private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"];
    private static readonly string[] MonthFormats = ["MM/yyyy", "M/yyyy", "yyyy-MM"];

    public bool TryParseDaysSince(string? text, out int days)
    {
        days = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = TextNormalizer.Normalize(text);
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

        if (DateOnly.TryParseExact(normalized, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || DateOnly.TryParseExact(normalized, MonthFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            days = today.DayNumber - date.DayNumber;
            return days is >= 0 and <= MaxDays;
        }

        var match = RelativePattern().Match(normalized);
        if (!match.Success)
            return false;

        int amount = int.TryParse(match.Groups["amount"].Value, out var n) ? n : 1;
        var unit = match.Groups["unit"].Value;
        int unitDays = unit.StartsWith("dia") || unit.StartsWith("day") ? 1
            : unit.StartsWith("semana") || unit.StartsWith("week") ? 7
            : unit.StartsWith("mes") || unit.StartsWith("month") ? 30
            : 365;

        days = amount * unitDays;
        return days <= MaxDays;
    }

    [GeneratedRegex(@"\b(?<amount>\d{1,3}|um|uma|a|an|one)\s*(?<unit>dias?|semanas?|mes(?:es)?|anos?|days?|weeks?|months?|years?)\b")]
    private static partial Regex RelativePattern();
}
