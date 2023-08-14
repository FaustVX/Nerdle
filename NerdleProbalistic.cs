class NerdleProbalistic : Nerdle
{
    private readonly float[,] _probalities = default!;
    private readonly Dictionary<char, int> _indices = default!;
    public required float[,] Probalities
    {
        get => _probalities;
        init
        {
            if (value.GetLength(0) is var l && !(l == InitialSymbols.Length && l == value.GetLength(1)))
                throw new System.Diagnostics.UnreachableException();
            _probalities = value;
            _indices = InitialSymbols
                .Select(static (s, i) => (s.symbol, i))
                .ToDictionary(static s => s.symbol, static s => s.i);
        }
    }

    public required float MinProb { get; init; }

    protected override Func<string, bool> GetValidator(char[][] symbols)
    => base.GetValidator(symbols).Then((input, b) => b && ProbalitiesFor(input));

    bool ProbalitiesFor(string line)
    {
        for (var i = 1; i < line.Length; i++)
            if (_probalities[_indices[line[i - 1]], _indices[line[i]]] < MinProb)
                return false;
        return true;
    }

    public static float[,] CreateMarkovChain(IReadOnlySet<string> lines)
    => CreateMarkovChain(lines, lines.SelectMany(static w => w).ToHashSet().Order().ToHashSet());

    public static float[,] CreateMarkovChain(IReadOnlySet<string> lines, IReadOnlySet<char> allowedLetters)
    {
        var indices = allowedLetters.Select(static (c, i) => (c, i)).ToDictionary(static t => t.c, t => t.i);
        var count = new int[allowedLetters.Count, allowedLetters.Count];
        var sum = new int[allowedLetters.Count];
        foreach (var line in lines)
            for (var i = 1; i < line.Length; i++)
            {
                count[indices[line[i - 1]], indices[line[i]]]++;
                sum[indices[line[i - 1]]]++;
            }
        var probalities = new float[allowedLetters.Count, allowedLetters.Count];
        for (int i = 0; i < allowedLetters.Count; i++)
            for (int j = 0; j < allowedLetters.Count; j++)
                probalities[i, j] = count[i, j] / (float)sum[i];
        return probalities;
    }
}
