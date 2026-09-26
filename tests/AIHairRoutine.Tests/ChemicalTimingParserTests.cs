using Shouldly;

namespace AIHairRoutine.Tests;

public sealed class ChemicalTimingParserTests
{
    // Clock: 2026-09-25.
    [Theory]
    [InlineData("há 2 meses", 60)]
    [InlineData("Há 2 Meses", 60)]
    [InlineData("3 semanas", 21)]
    [InlineData("faz uma semana", 7)]
    [InlineData("1 ano", 365)]
    [InlineData("10 dias atrás", 10)]
    [InlineData("2 months ago", 60)]
    [InlineData("10/06/2026", 107)]
    [InlineData("2026-06-10", 107)]
    [InlineData("06/2026", 116)]
    [InlineData("25/09/2026", 0)]
    public void Resolves_relative_text_and_dates_into_days(string text, int expectedDays)
    {
        TestData.Timing.TryParseDaysSince(text, out var days).ShouldBeTrue();
        days.ShouldBe(expectedDays);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("não lembro")]
    [InlineData("10/12/2026")] // future
    [InlineData("32/01/2026")] // invalid date
    public void Rejects_unparseable_or_future_answers(string? text)
    {
        TestData.Timing.TryParseDaysSince(text, out _).ShouldBeFalse();
    }
}
