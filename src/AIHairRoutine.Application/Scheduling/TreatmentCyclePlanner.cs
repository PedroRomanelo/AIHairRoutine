using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Application.Scheduling;

/// <summary>
/// Distributes the treatment slots of the 4-week cycle among hydration, nutrition and reconstruction
/// proportionally to the profile's needs, under the classic cronograma rules:
/// at most one reconstruction per week, never two reconstructions in a row, and weekly hydration
/// for curly/coily hair.
/// </summary>
public static class TreatmentCyclePlanner
{
    private const int H = (int)TreatmentType.Hydration;
    private const int N = (int)TreatmentType.Nutrition;
    private const int R = (int)TreatmentType.Reconstruction;

    /// <returns>One array per week, with the treatment of each slot in order.</returns>
    public static TreatmentType[][] Plan(HairProfile profile, int slotsPerWeek, int weeks = ScheduleBuilder.CycleWeeks)
    {
        if (slotsPerWeek <= 0)
            return Enumerable.Range(0, weeks).Select(_ => Array.Empty<TreatmentType>()).ToArray();

        var needs = profile.TreatmentNeeds;
        var counts = Allocate(needs, weeks * slotsPerWeek);

        bool weeklyHydration = profile.HairType is HairType.Curly or HairType.Coily && slotsPerWeek >= 2;
        int minHydration = weeklyHydration ? weeks : 1;

        // With one slot a week, "never two in a row" means reconstruction every other week at most.
        int maxReconstruction = slotsPerWeek == 1 ? weeks / 2 : weeks;

        while (counts[H] < minHydration)
        {
            int donor = counts[N] >= counts[R] ? N : R;
            if (counts[donor] == 0)
                break;
            counts[donor]--;
            counts[H]++;
        }

        while (counts[R] > maxReconstruction)
        {
            counts[R]--;
            counts[needs.Hydration >= needs.Nutrition ? H : N]++;
        }

        return Place(counts, weeks, slotsPerWeek, weeklyHydration);
    }

    /// <summary>Largest-remainder allocation of <paramref name="total"/> slots by need share.</summary>
    private static int[] Allocate(TreatmentNeeds needs, int total)
    {
        double[] share = [needs.Hydration, needs.Nutrition, needs.Reconstruction];
        double sum = share.Sum();
        if (sum <= 0)
            share = [1, 0, 0];
        else
            share = share.Select(s => s / sum).ToArray();

        var exact = share.Select(s => s * total).ToArray();
        var counts = exact.Select(e => (int)Math.Floor(e)).ToArray();
        int remaining = total - counts.Sum();

        foreach (var i in Enumerable.Range(0, 3).OrderByDescending(i => exact[i] - counts[i]).ThenBy(i => i).Take(remaining))
            counts[i]++;

        return counts;
    }

    private static TreatmentType[][] Place(int[] counts, int weeks, int slotsPerWeek, bool weeklyHydration)
    {
        var grid = Enumerable.Range(0, weeks).Select(_ => new TreatmentType?[slotsPerWeek]).ToArray();

        // Reconstruction: first slot of evenly spread weeks. First-slot placement plus at most one per
        // week guarantees two reconstructions are never consecutive when there are 2+ slots a week.
        int reconstructions = counts[R];
        for (int i = 0; i < reconstructions; i++)
            grid[(int)Math.Floor((i + 0.5) * weeks / reconstructions)][0] = TreatmentType.Reconstruction;

        int hydrations = counts[H];
        int nutritions = counts[N];

        if (weeklyHydration)
        {
            foreach (var week in grid)
            {
                int free = Array.IndexOf(week, null);
                if (free < 0 || hydrations == 0)
                    continue;
                week[free] = TreatmentType.Hydration;
                hydrations--;
            }
        }

        // Fill the rest alternating hydration and nutrition, favoring whichever has more left.
        TreatmentType? previous = null;
        foreach (var week in grid)
        {
            for (int slot = 0; slot < week.Length; slot++)
            {
                if (week[slot] is { } existing)
                {
                    previous = existing;
                    continue;
                }

                var next = hydrations == 0 ? TreatmentType.Nutrition
                    : nutritions == 0 ? TreatmentType.Hydration
                    : hydrations > nutritions ? TreatmentType.Hydration
                    : nutritions > hydrations ? TreatmentType.Nutrition
                    : previous == TreatmentType.Hydration ? TreatmentType.Nutrition
                    : TreatmentType.Hydration;

                if (next == TreatmentType.Hydration) hydrations--;
                else nutritions--;

                week[slot] = next;
                previous = next;
            }
        }

        return grid.Select(week => week.Select(t => t!.Value).ToArray()).ToArray();
    }
}
