using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Infrastructure.Generation;

/// <summary>
/// Deterministic fallback generator. Produces a sensible, safe routine without any LLM,
/// so the endpoint keeps working (with degraded richness) when the provider is unavailable.
/// </summary>
public sealed class TemplateRoutineGenerator : IRoutineGenerator
{
    public Task<RoutineResult> GenerateAsync(
        ProfileResult profile,
        IReadOnlyList<ProductMatch> products,
        string locale,
        CancellationToken ct = default)
    {
        bool en = locale.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var p = profile.Profile;
        var priorities = profile.Priorities;

        int? Pick(params string[] categories) => products
            .Where(m => categories.Contains(m.Product.Category, StringComparer.OrdinalIgnoreCase))
            .Select(m => (int?)m.Product.Id)
            .FirstOrDefault();

        var steps = new List<RoutineStep>();
        int order = 1;

        string washFreq = p.Condition == HairCondition.Oily
            ? (en ? "every other day" : "dia sim, dia não")
            : (en ? "2-3x per week" : "2 a 3x por semana");

        steps.Add(new RoutineStep
        {
            Order = order++,
            Phase = "wash",
            Frequency = washFreq,
            ProductId = Pick("shampoo"),
            How = en ? "Massage into the scalp and rinse." : "Massageie no couro cabeludo e enxágue.",
            Why = en ? "Cleanses without stripping the hair." : "Limpa sem agredir os fios.",
        });

        steps.Add(new RoutineStep
        {
            Order = order++,
            Phase = "condition",
            Frequency = en ? "every wash" : "toda lavagem",
            ProductId = Pick("conditioner"),
            How = en ? "Apply from mid-length to ends, rinse." : "Aplique do meio às pontas e enxágue.",
            Why = en ? "Seals cuticles and adds slip." : "Sela as cutículas e dá deslize.",
        });

        if (priorities.Contains(HairPriority.Hydration) || priorities.Contains(HairPriority.DamageRepair))
        {
            steps.Add(new RoutineStep
            {
                Order = order++,
                Phase = "weekly",
                Frequency = en ? "1-2x per week" : "1 a 2x por semana",
                ProductId = Pick("mask"),
                How = en ? "Apply to damp hair, wait 5-15 min, rinse." : "Aplique no cabelo úmido, aguarde 5-15 min e enxágue.",
                Why = en ? "Deep treatment for the top priorities." : "Tratamento profundo para as prioridades principais.",
            });
        }

        if (priorities.Contains(HairPriority.FrizzControl))
        {
            steps.Add(new RoutineStep
            {
                Order = order++,
                Phase = "finish",
                Frequency = en ? "daily" : "diariamente",
                ProductId = Pick("leave_in", "oil"),
                How = en ? "Apply a small amount to damp ends." : "Aplique pouca quantidade nas pontas úmidas.",
                Why = en ? "Controls frizz and protects the fiber." : "Controla o frizz e protege o fio.",
            });
        }

        if (priorities.Contains(HairPriority.HairLossControl))
        {
            steps.Add(new RoutineStep
            {
                Order = order++,
                Phase = "treatment",
                Frequency = en ? "as directed" : "conforme indicação",
                ProductId = Pick("tonic"),
                How = en ? "Apply to the scalp and massage." : "Aplique no couro cabeludo e massageie.",
                Why = en ? "Supports the scalp against shedding." : "Apoia o couro cabeludo contra a queda.",
            });
        }

        var priorityLabels = string.Join(", ", priorities.Select(pr => PriorityLabel(pr, en)));
        var summary = en
            ? $"Routine focused on {priorityLabels} for {HairTypeLabel(p.HairType, true)} hair."
            : $"Rotina com foco em {priorityLabels} para cabelo {HairTypeLabel(p.HairType, false)}.";

        var tips = en
            ? new List<string> { "Avoid very hot water.", "Use heat protection before blow-drying." }
            : new List<string> { "Evite água muito quente.", "Use protetor térmico antes de secar." };

        var routine = new HairRoutine { Summary = summary, Steps = steps, Tips = tips };
        return Task.FromResult(new RoutineResult { Routine = routine, Model = "template", FromCache = false });
    }

    private static string PriorityLabel(HairPriority priority, bool en) => (priority, en) switch
    {
        (HairPriority.Hydration, false) => "hidratação",
        (HairPriority.FrizzControl, false) => "controle de frizz",
        (HairPriority.DamageRepair, false) => "reparação",
        (HairPriority.OilControl, false) => "controle de oleosidade",
        (HairPriority.HairLossControl, false) => "queda",
        (HairPriority.Hydration, true) => "hydration",
        (HairPriority.FrizzControl, true) => "frizz control",
        (HairPriority.DamageRepair, true) => "damage repair",
        (HairPriority.OilControl, true) => "oil control",
        (HairPriority.HairLossControl, true) => "hair loss",
        _ => priority.ToString(),
    };

    private static string HairTypeLabel(HairType type, bool en) => (type, en) switch
    {
        (HairType.Straight, false) => "liso",
        (HairType.Wavy, false) => "ondulado",
        (HairType.Curly, false) => "cacheado",
        (HairType.Coily, false) => "crespo",
        (HairType.Straight, true) => "straight",
        (HairType.Wavy, true) => "wavy",
        (HairType.Curly, true) => "curly",
        (HairType.Coily, true) => "coily",
        _ => type.ToString(),
    };
}
