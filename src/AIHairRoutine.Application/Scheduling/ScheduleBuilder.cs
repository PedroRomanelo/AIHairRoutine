using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Localization;
using AIHairRoutine.Application.Models;
using static System.DayOfWeek;

namespace AIHairRoutine.Application.Scheduling;

/// <summary>
/// Builds the 4-week cronograma deterministically:
/// wash days by hair type and oiliness → treatment slots per week → H/N/R cycle → one product per
/// slot (respecting the minimum interval between uses) → everyday products by category/frequency →
/// steps ordered by application order → grouped overview and special-care notes.
/// </summary>
public sealed class ScheduleBuilder : IScheduleBuilder
{
    public const int CycleWeeks = 4;

    private static readonly DayOfWeek[] WeekOrder = [Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday];

    private static readonly IReadOnlyDictionary<int, DayOfWeek[]> WashPatterns = new Dictionary<int, DayOfWeek[]>
    {
        [2] = [Monday, Thursday],
        [3] = [Monday, Wednesday, Saturday],
        [4] = [Monday, Wednesday, Friday, Sunday],
        [5] = [Monday, Tuesday, Thursday, Friday, Saturday],
        [6] = [Monday, Tuesday, Wednesday, Thursday, Friday, Saturday],
        [7] = WeekOrder,
    };

    /// <summary>Categories used on every wash day, regardless of their declared frequency.</summary>
    private static readonly ProductCategory[] WashDayCategories =
        [ProductCategory.Shampoo, ProductCategory.Conditioner, ProductCategory.LeaveIn];

    public HairSchedule Build(HairProfile profile, IReadOnlyList<ProductMatch> eligible, string locale)
    {
        bool en = Labels.IsEnglish(locale);

        var washDays = WashPatterns[WashDaysPerWeek(profile)];
        int slotsPerWeek = Math.Min(TreatmentSlotsPerWeek(profile), washDays.Length);
        var treatmentDays = Spread(washDays, slotsPerWeek);
        var cycle = TreatmentCyclePlanner.Plan(profile, slotsPerWeek);

        var plan = new CalendarPlan();
        PlaceTreatmentSlots(plan, cycle, washDays, treatmentDays, eligible, en);
        PlaceRoutineProducts(plan, washDays, eligible);

        return new HairSchedule
        {
            WashDays = washDays,
            TreatmentDays = treatmentDays,
            Weeks = BuildWeeks(plan, cycle, en),
            Overview = BuildOverview(plan, slotsPerWeek, en),
            SpecialCare = BuildSpecialCare(profile, plan, en),
        };
    }

    // --- Frequency rules ---

    /// <summary>Base washes a week by hair type; oily scalp washes more, dry hair less (2..7).</summary>
    public static int WashDaysPerWeek(HairProfile profile)
    {
        int days = profile.HairType switch
        {
            HairType.Straight => 4,
            HairType.Wavy => 3,
            HairType.Curly => 3,
            _ => 2,
        };

        if (profile.Has(HairCondition.Oily)) days += 2;
        if (profile.Has(HairCondition.Dry)) days -= 1;

        return Math.Clamp(days, 2, 7);
    }

    /// <summary>1 treatment a week; 2 for dry, damaged, curly or coily hair; oily hair is capped at 1.</summary>
    public static int TreatmentSlotsPerWeek(HairProfile profile)
    {
        if (profile.Has(HairCondition.Oily))
            return 1;

        bool needsMore = profile.Has(HairCondition.Dry)
            || profile.Has(HairCondition.Damaged)
            || profile.HairType is HairType.Curly or HairType.Coily;

        return needsMore ? 2 : 1;
    }

    // --- Placement ---

    private static void PlaceTreatmentSlots(
        CalendarPlan plan,
        TreatmentType[][] cycle,
        DayOfWeek[] washDays,
        DayOfWeek[] treatmentDays,
        IReadOnlyList<ProductMatch> eligible,
        bool en)
    {
        var candidates = eligible
            .Where(m => m.Product.Category is ProductCategory.Mask or ProductCategory.Treatment
                        && m.Product.TreatmentTypes.Count > 0)
            .ToList();

        for (int week = 0; week < cycle.Length; week++)
        {
            var taken = new HashSet<DayOfWeek>();

            for (int slot = 0; slot < cycle[week].Length; slot++)
            {
                var type = cycle[week][slot];
                var preferred = treatmentDays[slot];

                // If the interval rule blocks every product on the preferred day, try the next free wash days.
                var options = washDays
                    .Where(d => d == preferred || (Position(d) > Position(preferred) && !treatmentDays.Contains(d)))
                    .Where(d => !taken.Contains(d));

                bool placed = false;
                foreach (var day in options)
                {
                    int dayIndex = DayIndex(week, day);
                    var match = candidates
                        .Where(m => m.Product.TreatmentTypes.Contains(type) && plan.CanUse(m.Product, dayIndex))
                        .OrderByDescending(m => m.Score)
                        .ThenBy(m => m.Product.TreatmentTypes.Count) // on a tie, prefer the specialist
                        .FirstOrDefault();

                    if (match is null)
                        continue;

                    plan.Add(new Placement(week, day, match.Product, match.Product.Category, type, null, FromSlot: true));
                    taken.Add(day);
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    plan.Add(new Placement(week, preferred, null, ProductCategory.Mask, type, GenericMaskName(type, en), FromSlot: true));
                    taken.Add(preferred);
                }
            }
        }
    }

    private static void PlaceRoutineProducts(CalendarPlan plan, DayOfWeek[] washDays, IReadOnlyList<ProductMatch> eligible)
    {
        foreach (var product in PickRoutineProducts(eligible))
        {
            foreach (var (week, day) in Occurrences(product, washDays))
            {
                if (plan.CanUse(product, DayIndex(week, day)))
                    plan.Add(new Placement(week, day, product, product.Category, null, null, FromSlot: false));
            }
        }
    }

    /// <summary>One best product per role. Masks and H/N/R treatments are placed by the cycle instead.</summary>
    private static IEnumerable<Product> PickRoutineProducts(IReadOnlyList<ProductMatch> eligible)
    {
        Product? Best(Func<Product, bool> predicate) => eligible.FirstOrDefault(m => predicate(m.Product))?.Product;

        Product?[] picks =
        [
            Best(p => p.Category == ProductCategory.Shampoo),
            Best(p => p.Category == ProductCategory.Conditioner),
            Best(p => p.Category == ProductCategory.LeaveIn),
            Best(p => p.Category is ProductCategory.Serum or ProductCategory.Oil),
            Best(p => p.Category == ProductCategory.Finisher),
            Best(p => p.Category == ProductCategory.Treatment && p.TreatmentTypes.Count == 0),
        ];

        return picks.OfType<Product>();
    }

    private static IEnumerable<(int Week, DayOfWeek Day)> Occurrences(Product product, DayOfWeek[] washDays)
    {
        if (WashDayCategories.Contains(product.Category))
            return Every(washDays);

        var weeklyDay = washDays.Contains(Saturday) ? Saturday : washDays[^1];

        return product.UsageFrequency switch
        {
            UsageFrequency.Daily => Every(WeekOrder),
            UsageFrequency.TwiceWeekly => Every(Spread(washDays, Math.Min(2, washDays.Length))),
            UsageFrequency.Weekly => Every([weeklyDay]),
            UsageFrequency.Biweekly => [(0, weeklyDay), (2, weeklyDay)],
            UsageFrequency.Monthly => [(0, weeklyDay)],
            _ => [],
        };

        static IEnumerable<(int, DayOfWeek)> Every(DayOfWeek[] days) =>
            from week in Enumerable.Range(0, CycleWeeks)
            from day in days
            select (week, day);
    }

    // --- Output ---

    private static IReadOnlyList<ScheduleWeek> BuildWeeks(CalendarPlan plan, TreatmentType[][] cycle, bool en) =>
        Enumerable.Range(0, CycleWeeks)
            .Select(week => new ScheduleWeek
            {
                Number = week + 1,
                Focus = cycle[week].Distinct().ToList(),
                Days = WeekOrder
                    .Select(day => (Day: day, Placements: plan.Placements
                        .Where(p => p.Week == week && p.Day == day)
                        .OrderBy(p => p.Product?.ApplicationOrder ?? DefaultOrder(p.Category))
                        .ThenBy(p => DefaultOrder(p.Category))
                        .ToList()))
                    .Where(x => x.Placements.Count > 0)
                    .Select(x => new ScheduleDay
                    {
                        Day = x.Day,
                        Steps = x.Placements.Select((p, i) => ToStep(p, i + 1, en)).ToList(),
                    })
                    .ToList(),
            })
            .ToList();

    private static ScheduleStep ToStep(Placement p, int order, bool en)
    {
        if (p.Product is { } product)
        {
            return new ScheduleStep
            {
                Order = order,
                ProductId = product.Id,
                ProductName = product.Name,
                Category = product.Category,
                Treatment = p.Treatment,
                ActionMinutes = product.ActionTimeMinutes,
            };
        }

        var label = Labels.Of(p.Treatment!.Value, en);
        return new ScheduleStep
        {
            Order = order,
            ProductName = p.GenericName!,
            Category = p.Category,
            Treatment = p.Treatment,
            How = en
                ? $"After shampooing, apply a {label} mask you already trust and leave it on as directed on the label."
                : $"Após o shampoo, aplique uma máscara de {label} que seja segura para você e deixe agir conforme a embalagem.",
            Why = en ? $"{Labels.Capitalize(label)} step of your schedule." : $"Etapa de {label} do seu cronograma.",
        };
    }

    private static IReadOnlyList<ScheduleGroup> BuildOverview(CalendarPlan plan, int slotsPerWeek, bool en)
    {
        var groups = new List<(int Rank, bool IsCycle, ScheduleGroup Group)>();

        // Everyday products: grouped by the weekdays they are used on.
        var byPattern = new Dictionary<string, (int Rank, string Label, List<DayOfWeek> Days, List<ScheduleGroupItem> Items)>();
        foreach (var uses in plan.Placements.Where(p => !p.FromSlot).GroupBy(p => p.Product!.Id))
        {
            var product = uses.First().Product!;
            var weekSets = Enumerable.Range(0, CycleWeeks)
                .Select(w => uses.Where(p => p.Week == w).Select(p => p.Day).ToHashSet())
                .ToList();
            var days = OrderDays(uses.Select(p => p.Day));
            bool regular = weekSets[0].Count > 0 && weekSets.All(s => s.SetEquals(weekSets[0]));

            var (key, rank, label) = regular
                ? (string.Join(',', days), days.Count, Frequency(days.Count, days, en))
                : ($"irregular:{product.UsageFrequency}:{string.Join(',', days)}", 0, Irregular(product.UsageFrequency, days, en));

            if (!byPattern.TryGetValue(key, out var group))
                byPattern[key] = group = (rank, label, days, []);

            group.Items.Add(new ScheduleGroupItem
            {
                ProductId = product.Id,
                Name = product.Name,
                ActionMinutes = product.ActionTimeMinutes,
                Weeks = uses.Select(p => p.Week + 1).Distinct().Order().ToList(),
            });
        }

        groups.AddRange(byPattern.Values.Select(g =>
            (g.Rank, false, new ScheduleGroup { Label = g.Label, Days = g.Days, Items = g.Items })));

        // H/N/R treatments rotate by week, so they are shown together on the treatment days.
        var slots = plan.Placements.Where(p => p.FromSlot).ToList();
        if (slots.Count > 0)
        {
            var days = OrderDays(slots.Select(p => p.Day));
            var frequency = Frequency(slotsPerWeek, days, en);
            groups.Add((days.Count, true, new ScheduleGroup
            {
                Label = en ? $"H/N/R schedule — {frequency}" : $"Cronograma H/N/R — {frequency}",
                Days = days,
                Items = slots
                    .GroupBy(p => p.Product?.Id.ToString() ?? $"generic:{p.Treatment}")
                    .Select(g => new ScheduleGroupItem
                    {
                        ProductId = g.First().Product?.Id,
                        Name = g.First().Product?.Name ?? g.First().GenericName!,
                        ActionMinutes = g.First().Product?.ActionTimeMinutes,
                        Treatments = g.Select(p => p.Treatment!.Value).Distinct().Order().ToList(),
                        Weeks = g.Select(p => p.Week + 1).Distinct().Order().ToList(),
                    })
                    .ToList(),
            }));
        }

        return groups
            .OrderByDescending(g => g.Rank)
            .ThenBy(g => g.IsCycle)
            .Select(g => g.Group)
            .ToList();
    }

    private static IReadOnlyList<string> BuildSpecialCare(HairProfile profile, CalendarPlan plan, bool en)
    {
        var care = new List<string>
        {
            en ? "Avoid washing with very hot water: it dries the strands and stimulates oiliness."
               : "Evite lavar com água muito quente: resseca os fios e estimula a oleosidade.",
            en ? "Apply heat protection before blow-drying or flat ironing."
               : "Use protetor térmico antes de secador ou chapinha.",
        };

        var intervalProducts = plan.Placements
            .Where(p => p.FromSlot && p.Product is { MinIntervalDays: > 0 })
            .Select(p => p.Product!)
            .DistinctBy(p => p.Id);
        foreach (var product in intervalProducts)
        {
            care.Add(en
                ? $"Keep at least {product.MinIntervalDays} days between uses of {product.Name}."
                : $"Intervalo mínimo de {product.MinIntervalDays} dias entre usos de {product.Name}.");
        }

        var chemical = profile.Chemical;
        if (chemical.IsRecent)
        {
            var type = Labels.Of(chemical.Type ?? ChemicalType.Other, en);
            int since = chemical.DaysSince!.Value;
            int wait = ChemicalStatus.RecentThresholdDays - since;
            care.Add(en
                ? $"Your chemical treatment ({type}) is recent ({since} days ago): avoid another chemical process for at least {wait} more days and use only products safe for chemically treated hair."
                : $"Sua química ({type}) é recente (há {since} dias): evite outro procedimento químico por pelo menos mais {wait} dias e use só produtos seguros para cabelos com química.");
        }

        if (chemical.HasChemical && chemical.TouchUpFrequency is TouchUpFrequency.Monthly or TouchUpFrequency.Bimonthly)
        {
            care.Add(en
                ? "Frequent touch-ups: schedule them on a reconstruction week and never on the same day as a mask."
                : "Retoques frequentes: faça o retoque em uma semana de reconstrução e nunca no mesmo dia de uma máscara.");
        }

        if (profile.Allergies.Count > 0)
        {
            var allergens = Labels.JoinList(profile.Allergies.Select(a => Labels.Of(a, en)), en);
            care.Add(en
                ? $"Products containing {allergens} were left out. Still, patch-test any new product before using it."
                : $"Produtos com {allergens} foram excluídos. Mesmo assim, faça um teste de sensibilidade antes de usar um produto novo.");
        }

        if (profile.Has(HairCondition.Oily))
        {
            care.Add(en
                ? "Apply conditioner and masks from mid-lengths to ends, away from the roots."
                : "Aplique condicionador e máscaras do comprimento às pontas, longe da raiz.");
        }

        var missing = plan.Placements.Where(p => p.FromSlot && p.Product is null).Select(p => p.Treatment!.Value).Distinct().Order();
        foreach (var type in missing)
        {
            var label = Labels.Of(type, en);
            care.Add(en
                ? $"No {label} mask in the catalog fits your profile; use one of your choice that is safe for you."
                : $"Não encontramos no catálogo uma máscara de {label} compatível com seu perfil; use uma de sua preferência que seja segura para você.");
        }

        return care;
    }

    // --- Helpers ---

    private static string Frequency(int timesPerWeek, IReadOnlyList<DayOfWeek> days, bool en)
    {
        if (days.Count == 7)
            return en ? "Daily" : "Diário";

        var list = Labels.JoinList(days.Select(d => Labels.DayShort(d, en)), en);
        return (timesPerWeek, en) switch
        {
            (1, true) => $"Once a week ({list})",
            (1, false) => $"1x por semana ({list})",
            (_, true) => $"{timesPerWeek}x a week ({list})",
            _ => $"{timesPerWeek}x por semana ({list})",
        };
    }

    private static string Irregular(UsageFrequency frequency, IReadOnlyList<DayOfWeek> days, bool en)
    {
        var list = Labels.JoinList(days.Select(d => Labels.DayShort(d, en)), en);
        return (frequency, en) switch
        {
            (UsageFrequency.Biweekly, true) => $"Every other week ({list})",
            (UsageFrequency.Biweekly, false) => $"A cada 15 dias ({list})",
            (UsageFrequency.Monthly, true) => $"Once a month ({list})",
            (UsageFrequency.Monthly, false) => $"1x por mês ({list})",
            (_, true) => $"Occasionally ({list})",
            _ => $"Ocasional ({list})",
        };
    }

    private static string GenericMaskName(TreatmentType type, bool en) => en
        ? $"{Labels.Capitalize(Labels.Of(type, true))} mask (your choice)"
        : $"Máscara de {Labels.Of(type, false)} (de sua preferência)";

    /// <summary>Evenly picks <paramref name="count"/> days out of <paramref name="days"/>.</summary>
    private static DayOfWeek[] Spread(DayOfWeek[] days, int count) =>
        Enumerable.Range(0, count)
            .Select(i => days[(int)Math.Round(i * (double)days.Length / count, MidpointRounding.AwayFromZero)])
            .ToArray();

    private static List<DayOfWeek> OrderDays(IEnumerable<DayOfWeek> days) => days.Distinct().OrderBy(Position).ToList();

    private static int Position(DayOfWeek day) => Array.IndexOf(WeekOrder, day);

    private static int DayIndex(int week, DayOfWeek day) => week * 7 + Position(day);

    /// <summary>Fallback application order when a step has no product.</summary>
    private static int DefaultOrder(ProductCategory category) => category switch
    {
        ProductCategory.Shampoo => 1,
        ProductCategory.Conditioner => 2,
        ProductCategory.Mask => 3,
        ProductCategory.Treatment => 4,
        ProductCategory.LeaveIn => 5,
        ProductCategory.Serum or ProductCategory.Oil => 6,
        _ => 7,
    };

    private sealed record Placement(
        int Week,
        DayOfWeek Day,
        Product? Product,
        ProductCategory Category,
        TreatmentType? Treatment,
        string? GenericName,
        bool FromSlot);

    /// <summary>The calendar being assembled, tracking each product's uses to enforce the minimum interval.</summary>
    private sealed class CalendarPlan
    {
        private readonly Dictionary<Guid, List<int>> _usesByProduct = [];

        public List<Placement> Placements { get; } = [];

        public bool CanUse(Product product, int dayIndex) =>
            !_usesByProduct.TryGetValue(product.Id, out var uses)
            || uses.All(u => Math.Abs(dayIndex - u) >= Math.Max(product.MinIntervalDays, 1));

        public void Add(Placement placement)
        {
            Placements.Add(placement);
            if (placement.Product is { } product)
            {
                if (!_usesByProduct.TryGetValue(product.Id, out var uses))
                    _usesByProduct[product.Id] = uses = [];
                uses.Add(DayIndex(placement.Week, placement.Day));
            }
        }
    }
}
