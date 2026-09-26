using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Infrastructure.Generation;

/// <summary>
/// Builds the provider-agnostic prompt that asks the model to write the texts of an already-built
/// schedule as strict JSON. Products are referenced by short aliases (p1, p2...) instead of GUIDs,
/// which models copy unreliably; <see cref="RoutinePrompt.ProductAliases"/> maps them back.
/// </summary>
public sealed class RoutinePromptBuilder
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public RoutinePrompt Build(ProfileResult profile, HairSchedule schedule, IReadOnlyList<Product> products, string locale)
    {
        var aliases = products.Select((p, i) => (Alias: $"p{i + 1}", Product: p)).ToList();
        var aliasById = aliases.ToDictionary(a => a.Product.Id, a => a.Alias);
        var p = profile.Profile;

        var payload = new
        {
            locale,
            profile = new
            {
                hairType = p.HairType,
                thickness = p.Thickness,
                tone = p.Tone,
                conditions = p.Conditions,
                chemical = p.Chemical.HasChemical
                    ? new { type = p.Chemical.Type, daysAgo = p.Chemical.DaysSince, recent = p.Chemical.IsRecent }
                    : null,
                allergies = p.Allergies,
            },
            priorities = profile.Priorities,
            washDays = schedule.WashDays,
            weeks = schedule.Weeks.Select(w => new
            {
                week = w.Number,
                focus = w.Focus,
                days = w.Days.Select(d => new
                {
                    day = d.Day,
                    steps = d.Steps.Select(s => new
                    {
                        @ref = s.ProductId is { } id ? aliasById.GetValueOrDefault(id) : null,
                        name = s.ProductName,
                        category = s.Category,
                        treatment = s.Treatment,
                        minutes = s.ActionMinutes,
                    }),
                }),
            }),
            specialCare = schedule.SpecialCare,
            products = aliases.Select(a => new
            {
                @ref = a.Alias,
                name = a.Product.Name,
                brand = a.Product.Brand,
                category = a.Product.Category,
                treatmentTypes = a.Product.TreatmentTypes,
                targets = a.Product.Targets,
                keyIngredients = a.Product.KeyIngredients,
                minutes = a.Product.ActionTimeMinutes,
                description = a.Product.Description,
            }),
        };

        var user = new StringBuilder()
            .AppendLine($"locale: {locale}")
            .AppendLine("Dados (JSON):")
            .Append(JsonSerializer.Serialize(payload, Json))
            .ToString();

        return new RoutinePrompt(BuildSystem(), user, aliases.ToDictionary(a => a.Alias, a => a.Product.Id));
    }

    private static string BuildSystem() =>
        """
        Você é um tricologista que escreve as orientações de um cronograma capilar de 4 semanas.
        O cronograma (dias, frequências, ciclo de hidratação/nutrição/reconstrução e produtos) JÁ FOI DEFINIDO
        e NÃO pode ser alterado: não adicione, remova ou troque produtos, dias ou frequências.
        Escreva, para CADA produto (identificado pelo campo "ref"), como aplicar e por que ele está na rotina,
        um resumo curto do cronograma e dicas práticas coerentes com o perfil, sem contradizer os cuidados especiais.
        Responda EXCLUSIVAMENTE com um objeto JSON válido, sem texto fora do JSON, no formato:
        {
          "summary": "string curta",
          "products": [
            { "ref": "p1", "how": "como aplicar (inclua o tempo de ação quando houver)", "why": "por que este produto" }
          ],
          "tips": ["dica 1", "dica 2"]
        }
        Use somente os "ref" fornecidos. Escreva os textos no idioma indicado por "locale".
        """;
}

/// <summary>A built prompt plus the alias→product id map needed to read the answer back.</summary>
public sealed record RoutinePrompt(string System, string User, IReadOnlyDictionary<string, Guid> ProductAliases);
