class WordleProbabilistic : Wordle
{
    private readonly float[,] _probabilities = default!;
    private readonly Dictionary<char, int> _indices = default!;
    public required float[,] Probabilities
    {
        get => _probabilities;
        init
        {
            if (value.GetLength(0) is var l && !(l == InitialSymbols.Length && l == value.GetLength(1)))
                throw new System.Diagnostics.UnreachableException();
            _probabilities = value;
            _indices = InitialSymbols
                .Select(static (s, i) => (s.symbol, i))
                .ToDictionary(static s => s.symbol, static s => s.i);
        }
    }

    public required float MinProb { get; init; }

    protected override Func<char[], bool> GetValidator(char[][] symbols)
    => base.GetValidator(symbols).Then((input, b) => b && ProbabilitiesFor(input));

    bool ProbabilitiesFor(char[] line)
    {
        for (var i = 1; i < line.Length; i++)
            if (_probabilities[_indices[line[i - 1]], _indices[line[i]]] < MinProb)
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
        var probabilities = new float[allowedLetters.Count, allowedLetters.Count];
        for (var i = 0; i < allowedLetters.Count; i++)
            for (var j = 0; j < allowedLetters.Count; j++)
                probabilities[i, j] = count[i, j] / (float)sum[i];
        return probabilities;
    }
}
