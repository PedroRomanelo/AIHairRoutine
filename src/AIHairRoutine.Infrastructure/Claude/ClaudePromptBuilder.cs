using System.Text;
using System.Text.Json;
using AIHairRoutine.Application.Models;

namespace AIHairRoutine.Infrastructure.Claude;

/// <summary>Builder for the Claude system+user prompt that produces the routine as strict JSON.</summary>
public sealed class ClaudePromptBuilder
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public string BuildSystem() =>
        """
        Você é um tricologista que monta rotinas de cuidado capilar.
        Receberá um perfil capilar tipado, prioridades ordenadas e uma lista de produtos disponíveis.
        Monte uma rotina prática e segura usando SOMENTE os produtos fornecidos (referencie pelo campo "id").
        Responda EXCLUSIVAMENTE com um objeto JSON válido, sem texto fora do JSON, no formato:
        {
          "summary": "string curta",
          "steps": [
            { "order": 1, "phase": "wash|condition|treatment|finish|weekly",
              "frequency": "texto (ex.: 3x por semana)", "productId": 0,
              "how": "como aplicar", "why": "por que este passo" }
          ],
          "tips": ["dica 1", "dica 2"]
        }
        Escreva os textos no idioma indicado por "locale". Não invente produtos nem ids.
        """;

    public string BuildUser(ProfileResult profile, IReadOnlyList<ProductMatch> products, string locale)
    {
        var payload = new
        {
            locale,
            profile = profile.Profile,
            priorities = profile.Priorities.Select(p => p.ToString()),
            products = products.Select(m => new
            {
                id = m.Product.Id,
                name = m.Product.Name,
                brand = m.Product.Brand,
                category = m.Product.Category,
                targets = m.Product.Targets.Select(t => t.ToString()),
                forChemical = m.Product.ForChemical,
                description = m.Product.Description,
            }),
        };

        var sb = new StringBuilder();
        sb.AppendLine($"locale: {locale}");
        sb.AppendLine("Dados (JSON):");
        sb.Append(JsonSerializer.Serialize(payload, Json));
        return sb.ToString();
    }
}
