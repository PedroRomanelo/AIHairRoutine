using System.Globalization;
using System.Text.RegularExpressions;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Infrastructure.Data;
using Shouldly;
using Row = AIHairRoutine.Infrastructure.Data.ProductRepository.ProductRow;

namespace AIHairRoutine.Tests;

/// <summary>
/// The catalog stores multi-valued fields as snake_case CSV. Unknown list tokens are skipped, so a typo
/// (e.g. an allergen) would silently let a product through the allergy filter: the mapping is pinned here
/// and the real seed script is checked token by token.
/// </summary>
public sealed class ProductRepositoryTests
{
    private static Row MakeRow(
        string category = "shampoo",
        string usage = "daily",
        string? hairTypes = "all",
        string? targets = "hydration",
        string? treatments = "",
        string? contraindicated = "",
        string? ingredients = "",
        string? allergens = "") => new()
    {
        Id = Guid.Parse("3f2b8c1e-0000-4000-8000-000000000001"),
        Name = "Produto",
        Brand = "Marca",
        Description = "Descrição",
        Category = category,
        HairTypesCsv = hairTypes,
        TargetsCsv = targets,
        TreatmentTypesCsv = treatments,
        SafeForChemical = true,
        ContraindicatedChemicalsCsv = contraindicated,
        UsageFrequency = usage,
        ActionTimeMinutes = 20,
        ApplicationOrder = 3,
        MinIntervalDays = 3,
        KeyIngredientsCsv = ingredients,
        AllergensCsv = allergens,
        Price = 69.90m,
        SizeMl = 300,
    };

    [Fact]
    public void Maps_every_column_and_snake_case_token()
    {
        var product = ProductRepository.Map(MakeRow(
            category: "leave_in",
            usage: "twice_weekly",
            hairTypes: "wavy,curly,coily",
            targets: "hair_loss_control,frizz_control,oil_control",
            treatments: "hydration,nutrition",
            contraindicated: "relaxation,straightening",
            ingredients: "manteiga de karité,óleo de coco",
            allergens: "coconut_oil,nut_oils,wheat_protein,essential_oils"));

        product.Id.ShouldBe(Guid.Parse("3f2b8c1e-0000-4000-8000-000000000001"));
        product.Name.ShouldBe("Produto");
        product.Brand.ShouldBe("Marca");
        product.Description.ShouldBe("Descrição");
        product.Category.ShouldBe(ProductCategory.LeaveIn);
        product.UsageFrequency.ShouldBe(UsageFrequency.TwiceWeekly);
        product.HairTypes.ShouldBe([HairType.Wavy, HairType.Curly, HairType.Coily]);
        product.Targets.ShouldBe([HairPriority.HairLossControl, HairPriority.FrizzControl, HairPriority.OilControl]);
        product.TreatmentTypes.ShouldBe([TreatmentType.Hydration, TreatmentType.Nutrition]);
        product.ContraindicatedChemicals.ShouldBe([ChemicalType.Relaxation, ChemicalType.Straightening]);
        product.KeyIngredients.ShouldBe(["manteiga de karité", "óleo de coco"]);
        product.Allergens.ShouldBe([Allergen.CoconutOil, Allergen.NutOils, Allergen.WheatProtein, Allergen.EssentialOils]);
        product.SafeForChemical.ShouldBeTrue();
        product.ActionTimeMinutes.ShouldBe(20);
        product.ApplicationOrder.ShouldBe(3);
        product.MinIntervalDays.ShouldBe(3);
        product.Price.ShouldBe(69.90m);
        product.SizeMl.ShouldBe(300);
    }

    [Theory]
    [InlineData("all")]
    [InlineData("ALL")]
    [InlineData("")]
    [InlineData(null)]
    public void All_or_blank_hair_types_mean_every_hair_type(string? hairTypes)
    {
        ProductRepository.Map(MakeRow(hairTypes: hairTypes)).HairTypes.ShouldBeEmpty();
    }

    [Fact]
    public void Tolerates_spaces_empty_entries_and_case_in_lists()
    {
        var product = ProductRepository.Map(MakeRow(
            targets: " hydration , ,Frizz_Control,",
            ingredients: " glicerina ,, pantenol ",
            allergens: "Fragrance, SULFATE"));

        product.Targets.ShouldBe([HairPriority.Hydration, HairPriority.FrizzControl]);
        product.KeyIngredients.ShouldBe(["glicerina", "pantenol"]);
        product.Allergens.ShouldBe([Allergen.Fragrance, Allergen.Sulfate]);
    }

    [Fact]
    public void Skips_unknown_list_tokens_which_is_why_the_seed_is_checked_below()
    {
        ProductRepository.Map(MakeRow(allergens: "fragrance,pollen,coconut")).Allergens.ShouldBe([Allergen.Fragrance]);
    }

    [Theory]
    [InlineData("gel", "daily", "ProductCategory")]
    [InlineData("shampoo", "hourly", "UsageFrequency")]
    public void Rejects_an_unknown_category_or_usage_frequency(string category, string usage, string expectedType)
    {
        var ex = Should.Throw<InvalidOperationException>(() => ProductRepository.Map(MakeRow(category: category, usage: usage)));
        ex.Message.ShouldContain(expectedType);
    }

    // --- The real seed script (Script0004) ---

    private static readonly Lazy<IReadOnlyList<Dictionary<string, string?>>> SeedRows =
        new(() => SqlInsertParser.Parse(SqlInsertParser.ReadEmbeddedScript("Script0004_SeedProducts.sql")));

    private static Row ToRow(Dictionary<string, string?> r) => new()
    {
        Id = Guid.NewGuid(),
        Name = r["Name"]!,
        Brand = r["Brand"],
        Description = r["Description"],
        Category = r["Category"]!,
        HairTypesCsv = r["HairTypesCsv"],
        TargetsCsv = r["TargetsCsv"],
        TreatmentTypesCsv = r["TreatmentTypesCsv"],
        SafeForChemical = r["SafeForChemical"] == "1",
        ContraindicatedChemicalsCsv = r["ContraindicatedChemicalsCsv"],
        UsageFrequency = r["UsageFrequency"]!,
        ActionTimeMinutes = r["ActionTimeMinutes"] is { } minutes ? int.Parse(minutes, CultureInfo.InvariantCulture) : null,
        ApplicationOrder = int.Parse(r["ApplicationOrder"]!, CultureInfo.InvariantCulture),
        MinIntervalDays = int.Parse(r["MinIntervalDays"]!, CultureInfo.InvariantCulture),
        KeyIngredientsCsv = r["KeyIngredientsCsv"],
        AllergensCsv = r["AllergensCsv"],
        Price = decimal.Parse(r["Price"]!, CultureInfo.InvariantCulture),
        SizeMl = int.Parse(r["SizeMl"]!, CultureInfo.InvariantCulture),
    };

    private static int TokenCount(string? csv) =>
        (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(t => !t.Equals("all", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<(Dictionary<string, string?> Raw, Product Product)> SeedProducts() =>
        SeedRows.Value.Select(raw => (raw, ProductRepository.Map(ToRow(raw))));

    [Fact]
    public void Parses_every_row_of_the_seed()
    {
        var script = SqlInsertParser.ReadEmbeddedScript("Script0004_SeedProducts.sql");
        int rowsInScript = Regex.Matches(script, @"^\s*\(N'", RegexOptions.Multiline).Count;

        SeedRows.Value.Count.ShouldBe(rowsInScript);
        SeedRows.Value.Count.ShouldBeGreaterThan(0);
        SeedRows.Value.Select(r => r["Name"]).ShouldBeUnique();
    }

    [Fact]
    public void Every_enum_token_in_the_seed_is_recognized()
    {
        foreach (var (raw, product) in SeedProducts())
        {
            var name = raw["Name"];
            product.HairTypes.Count.ShouldBe(TokenCount(raw["HairTypesCsv"]), $"hair types of {name}");
            product.Targets.Count.ShouldBe(TokenCount(raw["TargetsCsv"]), $"targets of {name}");
            product.TreatmentTypes.Count.ShouldBe(TokenCount(raw["TreatmentTypesCsv"]), $"H/N/R of {name}");
            product.ContraindicatedChemicals.Count.ShouldBe(TokenCount(raw["ContraindicatedChemicalsCsv"]), $"contraindications of {name}");
            product.Allergens.Count.ShouldBe(TokenCount(raw["AllergensCsv"]), $"allergens of {name}");
        }
    }

    [Fact]
    public void Seed_ingredients_are_tagged_with_the_allergens_they_contain()
    {
        (string Ingredient, Allergen Allergen)[] rules =
        [
            ("coco", Allergen.CoconutOil),
            ("argan", Allergen.NutOils),
            ("macadâmia", Allergen.NutOils),
            ("castanha", Allergen.NutOils),
            ("trigo", Allergen.WheatProtein),
            ("lanolina", Allergen.Lanolin),
            ("dimeticona", Allergen.Silicone),
            ("siloxano", Allergen.Silicone),
            ("parabeno", Allergen.Paraben),
        ];

        foreach (var (_, product) in SeedProducts())
        {
            foreach (var (ingredient, allergen) in rules)
            {
                if (product.KeyIngredients.Any(i => i.Contains(ingredient, StringComparison.OrdinalIgnoreCase)))
                    product.Allergens.ShouldContain(allergen, $"{product.Name} lists '{ingredient}'");
            }
        }
    }

    [Fact]
    public void Every_seed_mask_serves_at_least_one_treatment_axis()
    {
        SeedProducts()
            .Select(p => p.Product)
            .Where(p => p.Category == ProductCategory.Mask)
            .ShouldAllBe(p => p.TreatmentTypes.Count > 0);
    }

    [Theory]
    [InlineData(ProductCategory.Shampoo)]
    [InlineData(ProductCategory.Conditioner)]
    [InlineData(ProductCategory.Mask)]
    [InlineData(ProductCategory.LeaveIn)]
    [InlineData(ProductCategory.Oil)]
    public void The_seed_offers_an_allergen_free_option_per_core_category(ProductCategory category)
    {
        SeedProducts().ShouldContain(p => p.Product.Category == category && p.Product.Allergens.Count == 0);
    }
}

/// <summary>
/// Minimal parser for the seed's "INSERT INTO t (columns) VALUES (...), (...);" statement: handles
/// N'' strings with '' escapes, NULL, numbers and -- comments. One dictionary per row (column → literal).
/// </summary>
internal static class SqlInsertParser
{
    private enum Kind { Punct, Str, Word }

    private sealed record Token(Kind Kind, string Text);

    public static string ReadEmbeddedScript(string fileName)
    {
        var assembly = typeof(ProductRepository).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith(fileName, StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        return reader.ReadToEnd();
    }

    public static IReadOnlyList<Dictionary<string, string?>> Parse(string sql)
    {
        var tokens = Tokenize(sql);

        int insert = tokens.FindIndex(t => t.Kind == Kind.Word && t.Text.Equals("INSERT", StringComparison.OrdinalIgnoreCase));
        int open = tokens.FindIndex(insert, t => t.Text == "(");
        int close = tokens.FindIndex(open, t => t.Text == ")");
        var columns = tokens.Skip(open + 1).Take(close - open - 1).Where(t => t.Kind == Kind.Word).Select(t => t.Text).ToList();

        int i = tokens.FindIndex(close, t => t.Kind == Kind.Word && t.Text.Equals("VALUES", StringComparison.OrdinalIgnoreCase)) + 1;
        var rows = new List<Dictionary<string, string?>>();

        while (i < tokens.Count)
        {
            if (tokens[i].Text != "(") { i++; continue; }

            var values = new List<string?>();
            for (i++; tokens[i].Text != ")"; i++)
            {
                if (tokens[i].Kind == Kind.Punct)
                    continue; // separators
                values.Add(tokens[i].Kind == Kind.Word && tokens[i].Text.Equals("NULL", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : tokens[i].Text);
            }

            if (values.Count != columns.Count)
                throw new InvalidOperationException($"Row {rows.Count + 1} has {values.Count} values for {columns.Count} columns.");

            rows.Add(columns.Zip(values).ToDictionary(p => p.First, p => p.Second));
            i++;
        }

        return rows;
    }

    private static List<Token> Tokenize(string sql)
    {
        var tokens = new List<Token>();
        int i = 0;

        while (i < sql.Length)
        {
            char c = sql[i];
            char next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (char.IsWhiteSpace(c) || c == ';')
            {
                i++;
            }
            else if (c == '-' && next == '-')
            {
                while (i < sql.Length && sql[i] != '\n') i++;
            }
            else if ((c is 'N' or 'n') && next == '\'' && (i == 0 || !char.IsLetterOrDigit(sql[i - 1])))
            {
                i++; // unicode prefix of N'...'
            }
            else if (c == '\'')
            {
                var value = new System.Text.StringBuilder();
                for (i++; i < sql.Length; i++)
                {
                    if (sql[i] == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'') { value.Append('\''); i++; }
                    else if (sql[i] == '\'') { i++; break; }
                    else value.Append(sql[i]);
                }
                tokens.Add(new Token(Kind.Str, value.ToString()));
            }
            else if (c is '(' or ')' or ',')
            {
                tokens.Add(new Token(Kind.Punct, c.ToString()));
                i++;
            }
            else
            {
                int start = i;
                while (i < sql.Length && !char.IsWhiteSpace(sql[i]) && sql[i] is not ('(' or ')' or ',' or ';' or '\''))
                    i++;
                tokens.Add(new Token(Kind.Word, sql[start..i]));
            }
        }

        return tokens;
    }
}
