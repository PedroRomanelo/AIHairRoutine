using AIHairRoutine.Application.Matching;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Application.Scheduling;
using Shouldly;

namespace AIHairRoutine.Tests;

public sealed class ScheduleBuilderTests
{
    private readonly ProductSelector _selector = new();
    private readonly ScheduleBuilder _sut = new();

    private HairSchedule Build(HairAssessment assessment, IReadOnlyList<Product>? catalog = null)
    {
        var profile = TestData.Rules.Profile(assessment);
        var selection = _selector.Select(profile.Profile, profile.Priorities, catalog ?? TestCatalog.All);
        return _sut.Build(profile.Profile, selection.Eligible, assessment.Locale);
    }

    private static IEnumerable<(int Week, DayOfWeek Day, ScheduleStep Step)> Steps(HairSchedule schedule) =>
        from week in schedule.Weeks
        from day in week.Days
        from step in day.Steps
        select (week.Number, day.Day, step);

    private static int DayIndex(int week, DayOfWeek day) => (week - 1) * 7 + ((int)day + 6) % 7;

    /// <summary>Treatment-slot steps in calendar order.</summary>
    private static List<(int Week, TreatmentType Treatment)> Slots(HairSchedule schedule) =>
        Steps(schedule)
            .Where(x => x.Step.Treatment is not null)
            .OrderBy(x => DayIndex(x.Week, x.Day))
            .Select(x => (x.Week, x.Step.Treatment!.Value))
            .ToList();

    [Theory]
    [InlineData(HairType.Straight, HairCondition.Normal, 4)]
    [InlineData(HairType.Straight, HairCondition.Oily, 6)]
    [InlineData(HairType.Wavy, HairCondition.Normal, 3)]
    [InlineData(HairType.Coily, HairCondition.Dry, 2)]
    public void Wash_days_depend_on_hair_type_and_oiliness(HairType hairType, HairCondition condition, int expected)
    {
        var schedule = Build(TestData.Assessment(hairType, condition));

        schedule.Weeks.Count.ShouldBe(4);
        schedule.WashDays.Count.ShouldBe(expected);
        Steps(schedule).Where(x => x.Step.Category == ProductCategory.Shampoo)
            .ShouldAllBe(x => schedule.WashDays.Contains(x.Day));
    }

    [Fact]
    public void Dry_hair_gets_two_treatments_every_week()
    {
        var schedule = Build(TestData.Assessment(HairType.Straight, HairCondition.Dry));

        Slots(schedule).GroupBy(s => s.Week).ShouldAllBe(week => week.Count() == 2);
    }

    [Fact]
    public void Oily_hair_is_limited_to_one_treatment_per_week()
    {
        var schedule = Build(TestData.Assessment(HairType.Curly, HairCondition.Oily));

        Slots(schedule).GroupBy(s => s.Week).ShouldAllBe(week => week.Count() == 1);
    }

    [Fact]
    public void Curly_hair_gets_hydration_every_week()
    {
        var schedule = Build(TestData.Assessment(HairType.Curly, HairCondition.Dry));

        schedule.Weeks.ShouldAllBe(w => w.Focus.Contains(TreatmentType.Hydration));
    }

    [Fact]
    public void Reconstruction_happens_at_most_once_a_week_and_never_twice_in_a_row()
    {
        var assessment = TestData.Assessment(HairType.Straight, HairCondition.Damaged, HairCondition.Dry) with
        {
            Chemical = TestData.Chemical(ChemicalType.Bleaching, "há 20 dias"),
        };

        var slots = Slots(Build(assessment));

        slots.ShouldContain(s => s.Treatment == TreatmentType.Reconstruction);
        slots.GroupBy(s => s.Week)
            .ShouldAllBe(week => week.Count(s => s.Treatment == TreatmentType.Reconstruction) <= 1);
        slots.Zip(slots.Skip(1))
            .ShouldNotContain(pair => pair.First.Treatment == TreatmentType.Reconstruction
                                      && pair.Second.Treatment == TreatmentType.Reconstruction);
    }

    [Fact]
    public void Respects_the_minimum_interval_between_uses_of_the_same_product()
    {
        var byId = TestCatalog.All.ToDictionary(p => p.Id);
        var schedule = Build(TestData.Assessment(HairType.Coily, HairCondition.Dry, HairCondition.Damaged));

        foreach (var uses in Steps(schedule).Where(x => x.Step.ProductId is not null).GroupBy(x => x.Step.ProductId!.Value))
        {
            int interval = Math.Max(byId[uses.Key].MinIntervalDays, 1);
            var days = uses.Select(x => DayIndex(x.Week, x.Day)).Order().ToList();
            days.Zip(days.Skip(1)).ShouldAllBe(pair => pair.Second - pair.First >= interval);
        }
    }

    [Fact]
    public void Orders_each_day_by_application_order()
    {
        var byId = TestCatalog.All.ToDictionary(p => p.Id);
        var schedule = Build(TestData.Assessment(HairType.Curly, HairCondition.Dry, HairCondition.Frizzy));

        foreach (var day in schedule.Weeks.SelectMany(w => w.Days))
        {
            var orders = day.Steps.Where(s => s.ProductId is not null).Select(s => byId[s.ProductId!.Value].ApplicationOrder).ToList();
            orders.ShouldBe(orders.Order());
            day.Steps.Select(s => s.Order).ShouldBe(Enumerable.Range(1, day.Steps.Count));
        }
    }

    [Fact]
    public void A_multi_treatment_product_fills_slots_of_different_types()
    {
        var twoInOne = TestCatalog.Make("Máscara 2 em 1", ProductCategory.Mask,
            targets: [HairPriority.Hydration, HairPriority.Nutrition],
            treatments: [TreatmentType.Hydration, TreatmentType.Nutrition], minutes: 20, order: 3, minInterval: 3);
        IReadOnlyList<Product> catalog = [TestCatalog.ShampooFragranceFree, TestCatalog.Conditioner, twoInOne];

        var schedule = Build(TestData.Assessment(HairType.Curly, HairCondition.Dry), catalog);

        var treatments = Steps(schedule).Where(x => x.Step.ProductId == twoInOne.Id).Select(x => x.Step.Treatment).ToHashSet();
        treatments.ShouldContain(TreatmentType.Hydration);
        treatments.ShouldContain(TreatmentType.Nutrition);
    }

    [Fact]
    public void Uses_a_generic_step_when_no_product_fits_the_slot()
    {
        IReadOnlyList<Product> catalog = [TestCatalog.ShampooFragranceFree, TestCatalog.Conditioner, TestCatalog.HydrationMask];

        var schedule = Build(TestData.Assessment(HairType.Straight, HairCondition.Damaged), catalog);

        var generic = Steps(schedule).First(x => x.Step.Treatment == TreatmentType.Reconstruction).Step;
        generic.ProductId.ShouldBeNull();
        generic.ProductName.ShouldContain("reconstrução");
        generic.How.ShouldNotBeEmpty();
        schedule.SpecialCare.ShouldContain(c => c.Contains("máscara de reconstrução"));
    }

    [Fact]
    public void Overview_groups_products_by_weekday_pattern()
    {
        var schedule = Build(TestData.Assessment(HairType.Wavy, HairCondition.Normal));

        var washGroup = schedule.Overview.Single(g => g.Items.Any(i => i.ProductId == TestCatalog.Conditioner.Id));
        washGroup.Label.ShouldBe("3x por semana (seg, qua e sáb)");
        washGroup.Items.ShouldContain(i => i.Name.StartsWith("Shampoo"));

        schedule.Overview.ShouldContain(g => g.Label.StartsWith("Cronograma H/N/R — 1x por semana"));
    }

    [Fact]
    public void Writes_labels_and_special_care_in_english_for_en_locales()
    {
        var schedule = Build(TestData.Assessment(HairType.Wavy, HairCondition.Normal) with { Locale = "en-US" });

        schedule.Overview.ShouldContain(g => g.Label == "3x a week (Mon, Wed and Sat)");
        schedule.SpecialCare.ShouldContain(c => c.StartsWith("Avoid washing"));
    }
}
