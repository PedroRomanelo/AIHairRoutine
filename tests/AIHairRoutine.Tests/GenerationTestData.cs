using AIHairRoutine.Application.Abstractions;
using AIHairRoutine.Application.Models;
using AIHairRoutine.Infrastructure.Config;
using AIHairRoutine.Infrastructure.Generation.Providers;

namespace AIHairRoutine.Tests;

/// <summary>A minimal schedule with two products (aliases p1 = shampoo, p2 = mask) for the generator tests.</summary>
internal static class GenerationTestData
{
    public static readonly Product Shampoo = TestCatalog.ShampooFragranceFree;
    public static readonly Product Mask = TestCatalog.HydrationMask;
    public static readonly IReadOnlyList<Product> Products = [Shampoo, Mask];

    public static ProfileResult Profile => TestData.Rules.Profile(TestData.Assessment());

    public static HairSchedule Schedule => new()
    {
        WashDays = [DayOfWeek.Monday],
        TreatmentDays = [DayOfWeek.Monday],
        Weeks =
        [
            new ScheduleWeek
            {
                Number = 1,
                Focus = [TreatmentType.Hydration],
                Days =
                [
                    new ScheduleDay
                    {
                        Day = DayOfWeek.Monday,
                        Steps =
                        [
                            new ScheduleStep { Order = 1, ProductId = Shampoo.Id, ProductName = Shampoo.Name, Category = Shampoo.Category },
                            new ScheduleStep
                            {
                                Order = 2, ProductId = Mask.Id, ProductName = Mask.Name, Category = Mask.Category,
                                Treatment = TreatmentType.Hydration, ActionMinutes = Mask.ActionTimeMinutes,
                            },
                        ],
                    },
                ],
            },
        ],
        Overview = [],
        SpecialCare = ["Evite lavar com água muito quente."],
    };

    public static RoutineResult Result(string model) => new()
    {
        Narrative = new RoutineNarrative { Summary = $"summary from {model}", ProductNotes = [] },
        Model = model,
    };
}

/// <summary>Chat client that answers with a canned text and records the prompt it received.</summary>
internal sealed class FakeChatModelClient(string response) : IChatModelClient
{
    public ChatPrompt? LastPrompt { get; private set; }

    public AiProvider Provider => AiProvider.Anthropic;

    public Task<string> CompleteAsync(ChatPrompt prompt, CancellationToken ct = default)
    {
        LastPrompt = prompt;
        return Task.FromResult(response);
    }
}

/// <summary>Routine generator driven by a delegate, recording how many times and with what it was called.</summary>
internal sealed class StubRoutineGenerator(Func<CancellationToken, Task<RoutineResult>> behavior) : IRoutineGenerator
{
    public int Calls { get; private set; }
    public HairSchedule? LastSchedule { get; private set; }
    public IReadOnlyList<Product>? LastProducts { get; private set; }
    public string? LastLocale { get; private set; }

    public static StubRoutineGenerator Returning(RoutineResult result) => new(_ => Task.FromResult(result));

    public static StubRoutineGenerator Throwing(Exception ex) => new(_ => Task.FromException<RoutineResult>(ex));

    public Task<RoutineResult> GenerateAsync(
        ProfileResult profile,
        HairSchedule schedule,
        IReadOnlyList<Product> products,
        string locale,
        CancellationToken ct = default)
    {
        Calls++;
        LastSchedule = schedule;
        LastProducts = products;
        LastLocale = locale;
        return behavior(ct);
    }
}
