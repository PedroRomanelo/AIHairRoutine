using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Localization;
using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Infrastructure.Generation;

/// <summary>
/// Deterministic fallback narrative. Writes a sensible summary, how/why for each product and tips
/// without any LLM, so the endpoint keeps working (with plainer text) when the provider is unavailable.
/// </summary>
public sealed class TemplateRoutineGenerator : IRoutineGenerator
{
    public Task<RoutineResult> GenerateAsync(
        ProfileResult profile,
        HairSchedule schedule,
        IReadOnlyList<Product> products,
        string locale,
        CancellationToken ct = default)
    {
        bool en = Labels.IsEnglish(locale);

        var narrative = new RoutineNarrative
        {
            Summary = Summary(profile, schedule, en),
            ProductNotes = products.Select(p => new ProductNote(p.Id, How(p, en), Why(p, en))).ToList(),
            Tips = Tips(profile.Profile, en),
        };

        return Task.FromResult(new RoutineResult { Narrative = narrative, Model = "template", FromCache = false });
    }

    private static string Summary(ProfileResult profile, HairSchedule schedule, bool en)
    {
        var hairType = Labels.Of(profile.Profile.HairType, en);
        var priorities = Labels.JoinList(profile.Priorities.Select(p => Labels.Of(p, en)), en);
        int washes = schedule.WashDays.Count;

        var cycle = Labels.JoinList(
            schedule.Weeks
                .SelectMany(w => w.Days)
                .SelectMany(d => d.Steps)
                .Where(s => s.Treatment is not null)
                .GroupBy(s => s.Treatment!.Value)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Count()}x {Labels.Of(g.Key, en)}"),
            en);

        return en
            ? $"4-week schedule for {hairType} hair focused on {priorities}: {washes} washes a week and {cycle} across the cycle."
            : $"Cronograma de 4 semanas para cabelo {hairType} com foco em {priorities}: {washes} lavagens por semana e {cycle} ao longo do ciclo.";
    }

    private static string How(Product p, bool en)
    {
        var minutes = p.ActionTimeMinutes;
        return p.Category switch
        {
            ProductCategory.Shampoo => en
                ? "Apply to a wet scalp, massage with your fingertips and rinse well."
                : "Aplique no couro cabeludo molhado, massageie com as pontas dos dedos e enxágue bem.",
            ProductCategory.Conditioner => en
                ? "Apply from mid-lengths to ends, detangle with your fingers and rinse."
                : "Aplique do comprimento às pontas, desembarace com os dedos e enxágue.",
            ProductCategory.Mask or ProductCategory.Treatment when p.TreatmentTypes.Count > 0 => en
                ? $"After shampooing, apply section by section, leave on for {Minutes(minutes, en)} and rinse."
                : $"Após o shampoo, aplique mecha a mecha, deixe agir {Minutes(minutes, en)} e enxágue.",
            ProductCategory.Treatment => en
                ? "Apply directly to the scalp and massage gently; do not rinse."
                : "Aplique diretamente no couro cabeludo e massageie suavemente; não enxágue.",
            ProductCategory.LeaveIn => en
                ? "On damp hair, apply from mid-lengths to ends; do not rinse."
                : "No cabelo úmido, aplique do comprimento às pontas, sem enxaguar.",
            ProductCategory.Serum => en
                ? "Apply a few drops to the ends, on damp or dry hair."
                : "Aplique poucas gotas nas pontas, com o cabelo úmido ou seco.",
            ProductCategory.Oil => en
                ? "Warm 1–2 drops between your palms and smooth over the ends."
                : "Espalhe 1 a 2 gotas nas palmas das mãos e aplique nas pontas.",
            _ => en
                ? "Apply to damp hair and style as you prefer (air-dry or diffuser)."
                : "Aplique no cabelo úmido e finalize como preferir (ao natural ou com difusor).",
        };
    }

    private static string Minutes(int? minutes, bool en) => minutes is { } m
        ? $"{m} min"
        : en ? "as directed on the label" : "conforme a embalagem";

    private static string Why(Product p, bool en)
    {
        if (p.TreatmentTypes.Count > 0)
        {
            var steps = Labels.JoinList(p.TreatmentTypes.Select(t => Labels.Of(t, en)), en);
            var effects = Labels.JoinList(p.TreatmentTypes.Select(t => TreatmentEffect(t, en)), en);
            return en
                ? $"{Labels.Capitalize(steps)} step of the schedule: {effects}."
                : $"Etapa de {steps} do cronograma: {effects}.";
        }

        var reason = p.Category switch
        {
            ProductCategory.Shampoo => en ? "Cleanses the scalp and preps the strands." : "Limpa o couro cabeludo e prepara os fios.",
            ProductCategory.Conditioner => en ? "Seals the cuticles and eases detangling." : "Sela as cutículas e facilita o desembaraço.",
            ProductCategory.LeaveIn => en ? "Protects and keeps hair moisturized through the day." : "Protege e mantém a hidratação ao longo do dia.",
            ProductCategory.Serum => en ? "Seals the ends and reduces frizz." : "Sela as pontas e reduz o frizz.",
            ProductCategory.Oil => en ? "Nourishes the ends and adds shine." : "Nutre as pontas e dá brilho.",
            ProductCategory.Finisher => en ? "Styles and keeps the result." : "Finaliza e mantém o resultado.",
            _ => en ? "Targeted treatment." : "Tratamento direcionado.",
        };

        if (p.Targets.Count == 0)
            return reason;

        var focus = Labels.JoinList(p.Targets.Select(t => Labels.Of(t, en)), en);
        return en ? $"{reason} Focus: {focus}." : $"{reason} Foco em {focus}.";
    }

    private static string TreatmentEffect(TreatmentType type, bool en) => (type, en) switch
    {
        (TreatmentType.Hydration, false) => "repõe água e maciez",
        (TreatmentType.Nutrition, false) => "repõe lipídios, reduz o frizz e devolve o brilho",
        (TreatmentType.Reconstruction, false) => "repõe massa e fortalece a fibra",
        (TreatmentType.Hydration, true) => "restores water and softness",
        (TreatmentType.Nutrition, true) => "restores lipids, tames frizz and brings back shine",
        _ => "restores mass and strengthens the fiber",
    };

    private static IReadOnlyList<string> Tips(HairProfile p, bool en)
    {
        var tips = new List<string>
        {
            en ? "Sleep on a satin pillowcase or with loosely tied hair to reduce friction."
               : "Durma com fronha de cetim ou com o cabelo preso frouxo para reduzir o atrito.",
            en ? "Trim the ends every 3 months to keep the strands healthy."
               : "Apare as pontas a cada 3 meses para manter a saúde dos fios.",
        };

        if (p.HairType is HairType.Curly or HairType.Coily)
            tips.Add(en ? "Style while hair is still very wet and avoid brushing it dry to keep your curls defined."
                        : "Finalize com o cabelo bem molhado e evite escovar a seco para manter os cachos definidos.");
        if (p.Has(HairCondition.Oily))
            tips.Add(en ? "Avoid touching the roots during the day and prefer lukewarm water."
                        : "Evite mexer na raiz ao longo do dia e prefira água morna.");
        if (p.Has(HairCondition.Dull))
            tips.Add(en ? "A final cold-water rinse helps seal the cuticle and adds shine."
                        : "Um último enxágue com água fria ajuda a selar a cutícula e dar brilho.");
        if (p.Thickness == HairThickness.Fine)
            tips.Add(en ? "Use small amounts of product so fine hair isn't weighed down."
                        : "Use pouca quantidade de produto para não pesar os fios finos.");

        return tips;
    }
}
