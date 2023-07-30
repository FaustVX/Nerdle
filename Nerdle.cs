class Nerdle
{
    public required (char? mandatory, char[]? forbiden)[] Slot { get; init; }
    private readonly (char symbol, int qty, int min)[] _symbols = default!;
    private readonly bool _isMath = default;
    public (char symbol, int qty, int min)[] InitialSymbols { get; private init; } = default!;
    public required (char symbol, int qty, int min)[] Symbols
    {
        get => _symbols;
        init => (InitialSymbols, _isMath, _symbols) = (value, value.Any(static s => s.symbol is '='), value
            .Where(static s => s is (_, > 0, _) or (_, _, >= 0))
            .Where(s => s.qty >= 0)
            .Where(s => s.qty == 0 || (Slot.Count(slot => slot.mandatory == s.symbol) is var qty && qty != s.qty))
            .ToArray());
    }

    public IEnumerable<string> GetAllLines(bool printMaxCombinatory = false, int steps = 100)
    {
        // System.Diagnostics.Debugger.Launch();
        var symbols = Enumerable.Repeat((Slot, Symbols), Slot.Length)
            .Select(static (p, pos) => p.Slot[pos] switch
            {
                (char m, _) => Enumerable.Repeat(m, 1),
                (_, char[] f) => p.Symbols.Select(static s => s.symbol).Except(f),
                _ => throw new System.Diagnostics.UnreachableException(),
            })
            .Select(Enumerable.ToArray)
            .ToArray();

        var combinatory = 1L;
        if (printMaxCombinatory)
            checked
            {
                for (var i = 0; i < symbols.Length; i++)
                {
#if DEBUG
                    Console.WriteLine($"[{i}]: {symbols[i].Length}");
#endif
                    combinatory *= symbols[i].Length;
                }
#if DEBUG
                Console.WriteLine($"Max combinatory: {combinatory}");
#endif
            }

        return Process(0, new char[Slot.Length], symbols)
        .ReportProgress(combinatory, steps)
        .Where(GetValidator(symbols));
    }

    protected virtual Func<string, bool> GetValidator(char[][] symbols)
    {
        if (_isMath)
            if (symbols.Any(static a => a.Any(static s => s is '(' or ')' or '²' or '³')))
                return IsValidLineMathComplete;
            else
                return IsValidLineMath;
        return line => CheckSymbolQty(line, Symbols);
    }

    static IEnumerable<string> Process(int pos, char[] line, char[][] symbols)
    {
        foreach (var symbol in symbols[pos])
        {
            line[pos] = symbol;
            if (pos < line.Length - 1)
                foreach (var l in Process(pos + 1, line, symbols))
                    yield return l;
            else
                yield return new(line);
        }
    }

    public static ISet<char> GetNextSymbol(string start, IEnumerable<string> symbols)
    => symbols.Where(s => s.StartsWith(start)).Select(s => s[start.Length]).ToHashSet();

    bool IsValidLineMath(string line)
    => line.IndexOf('=') is var index and >= 3
        && CheckSymbolOnTheRight(line.AsSpan()[(index + 1)..])
        && CheckSymbolOnTheLeft(line.AsSpan()[..(index + 1)])
        && CheckFirstAndLast(line)
        && CheckBeforeAndAfterOperator(line.AsSpan()[..(index + 1)])
        && CheckSymbolQty(line, Symbols);

    bool IsValidLineMathComplete(string line)
    => line.IndexOf('=') is var index and >= 3
        && CheckSymbolOnTheRight(line.AsSpan()[(index + 1)..])
        && CheckSymbolOnTheLeft(line.AsSpan()[..(index + 1)])
        && CheckFirstAndLast(line)
        && CheckBeforeAndAfterOperator(line.AsSpan()[..(index + 1)])
        && CheckSymbolQty(line, Symbols)
        && CheckParenthesis(line.AsSpan()[..(index + 1)])
        && CheckExponent(line.AsSpan()[..(index + 1)]);

    static bool CheckSymbolOnTheRight(ReadOnlySpan<char> line)
    {
        var a = line.IndexOf('+');
        var s = line.IndexOf('-'); // negative number are allowed
        var m = line.IndexOf('*');
        var d = line.IndexOf('/');
        var e = line.IndexOf('=');
        var l = line.IndexOf('(');
        var r = line.IndexOf(')');
        var q = line.IndexOf('²');
        var c = line.IndexOf('³');
        return !(a != -1 || s > 0 || m != -1 || d != -1 || e != -1 || l != -1 || r != -1 || q != -1 || c != -1);
    }

    static bool CheckSymbolOnTheLeft(ReadOnlySpan<char> line)
    {
        var a = line.IndexOf('+');
        var s = line.IndexOf('-');
        var m = line.IndexOf('*');
        var d = line.IndexOf('/');
        var l = line.IndexOf('(');
        var r = line.IndexOf(')');
        var q = line.IndexOf('²');
        var c = line.IndexOf('³');
        return a != -1 || s != -1 || m != -1 || d != -1|| l != -1 || r != -1 || q != -1 || c != -1;
    }

    static bool CheckFirstAndLast(string line)
    => line[0] is >= '0' and <= '9' or '(' or '-' && line[^1] is >= '0' and <= '9';

    static bool CheckBeforeAndAfterOperator(ReadOnlySpan<char> line)
    {
        for (var i = 1; i < line.Length; i++)
            if (line[i] is '+' or '*' or '/' && !(line[i - 1] is >= '0' and <= '9' or ')' or '²' or '³' && line[i + 1] is >= '0' and <= '9' or '(' or '-'))
                return false;
            else if (line[i] is '-' && !(line[i - 1] is >= '0' and <= '9' or ')' or '+' or '*' or '/' or '-' or '²' or '³' && line[i + 1] is >= '0' and <= '9' or '(' or '-'))
                return false;
        return true;
    }

    static bool CheckSymbolQty(string line, (char symbol, int qty, int min)[] symbols)
    {
        foreach (var symbol in symbols)
            switch (symbol)
            {
                case (_, 0, 0):
                    break;
                case (_, <= 0, var min):
                    if (line.Count(symbol.symbol.Equals) < min)
                        return false;
                    break;
                case (_, var qty, _):
                    if (line.Count(symbol.symbol.Equals) != qty)
                        return false;
                    break;
            }
        return true;
    }

    static bool CheckParenthesis(ReadOnlySpan<char> line)
    {
        var depth = line[0] is '(' ? 1 : 0;
        for (var i = 1; i < line.Length; i++)
        {
            var c = line[i];
            if (c is '(')
            {
                depth++;
                if (line[i - 1] is not ('+' or '-' or '*' or '/') || line[i + 1] is ')')
                    return false;
            }
            else if (c is ')')
            {
                depth--;
                if (line[i + 1] is not ('+' or '-' or '*' or '/' or '²' or '³'))
                    return false;
            }
            if (depth < 0)
                return false;
        }
        return depth == 0;
    }

    static bool CheckExponent(ReadOnlySpan<char> line)
    {
        for (var i = 1; i < line.Length - 1; i++)
            if (line[i] is '²' or '³' && !(line[i - 1] is (>= '0' and <= '9') or ')' && line[i + 1] is '+' or '-' or '*' or '/' or '='))
                return false;
        return true;
    }
}
