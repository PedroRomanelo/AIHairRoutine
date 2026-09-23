namespace AIHairRoutine.Infrastructure.Jev;

/// <summary>
/// Builder (fluent) for a JEV /v1/systemone request. Produces a plain dictionary payload,
/// so serialization never depends on polymorphic question shapes.
/// </summary>
public sealed class JevRequestBuilder
{
    private readonly Dictionary<string, object?> _questions = new();
    private object? _state;
    private string _model = "jev-latest";

    public JevRequestBuilder WithModel(string model)
    {
        _model = model;
        return this;
    }

    public JevRequestBuilder WithState(object state)
    {
        _state = state;
        return this;
    }

    /// <summary>Choice question: pick one key from <paramref name="criteria"/>.</summary>
    public JevRequestBuilder AddChoice(string name, string instructions, IReadOnlyDictionary<string, string> criteria)
    {
        _questions[name] = new Dictionary<string, object?>
        {
            ["type"] = "choice",
            ["instructions"] = instructions,
            ["criteria"] = criteria,
        };
        return this;
    }

    /// <summary>Score question: a position on the ordered <paramref name="criteria"/> scale.</summary>
    public JevRequestBuilder AddScore(string name, string instructions, IReadOnlyList<string> criteria)
    {
        _questions[name] = new Dictionary<string, object?>
        {
            ["type"] = "score",
            ["instructions"] = instructions,
            ["criteria"] = criteria,
        };
        return this;
    }

    /// <summary>Noul question: a yes/no probability.</summary>
    public JevRequestBuilder AddNoul(string name, string instructions)
    {
        _questions[name] = new Dictionary<string, object?>
        {
            ["type"] = "noul",
            ["instructions"] = instructions,
        };
        return this;
    }

    public Dictionary<string, object?> Build() => new()
    {
        ["model"] = _model,
        ["state"] = _state,
        ["questions"] = _questions,
    };
}
