using Optional;
using Optional.Unsafe;

class Nerdle<TSymbol>
where TSymbol : IEquatable<TSymbol>
{
    public required (Option<TSymbol> mandatory, TSymbol[]? forbiden)[] Slot { get; init; }
    protected readonly (TSymbol symbol, int? qty, int min)[] _symbols = default!;
    public (TSymbol symbol, int? qty, int min)[] InitialSymbols { get; private init; } = default!;
    public virtual required (TSymbol symbol, int? qty, int min)[] Symbols
    {
        get => _symbols;
        init => (InitialSymbols, _symbols) = (value, value
            .Where(static s => s is (_, > 0, _) or (_, _, >= 0))
            .Where(s => s.qty is > 0 or null)
            .Where(s => s.qty is null || (Slot.Count(slot => slot.mandatory.Contains(s.symbol)) is var qty && qty != s.qty))
            .ToArray());
    }

    public (IEnumerable<TSymbol[]> candidates, long maxQuantity) GetAllLines()
    {
        // System.Diagnostics.Debugger.Launch();
        var symbols = Enumerable.Repeat((Slot, Symbols), Slot.Length)
            .Select(static (p, pos) => p.Slot[pos] switch
            {
                ({ HasValue: true } m, _) => Enumerable.Repeat(m.ValueOrDefault(), 1),
                (_, TSymbol[] f) => p.Symbols.Select(static s => s.symbol).Except(f),
                _ => throw new System.Diagnostics.UnreachableException(),
            })
            .Select(Enumerable.ToArray)
            .ToArray();

        var combinatory = symbols.Aggregate(1L, static (acc, s) => checked(acc *= s.Length));

        return (Process(0, new TSymbol[Slot.Length], symbols).Where(GetValidator(symbols)), combinatory);
    }

    protected virtual Func<TSymbol[], bool> GetValidator(TSymbol[][] symbols)
    => line => CheckSymbolQty(line, Symbols);

    protected static IEnumerable<TSymbol[]> Process(int pos, TSymbol[] line, TSymbol[][] symbols)
    {
        foreach (var symbol in symbols[pos])
        {
            line[pos] = symbol;
            if (pos < line.Length - 1)
                foreach (var l in Process(pos + 1, line, symbols))
                    yield return l;
            else
                yield return (TSymbol[])line.Clone();
        }
    }

    protected static bool CheckSymbolQty(ReadOnlySpan<TSymbol> line, (TSymbol symbol, int? qty, int min)[] symbols)
    {
        foreach (var symbol in symbols)
            switch (symbol)
            {
                case (_, null, <= 0):
                    break;
                case (_, int qty, _):
                    if (line.Count(symbol.symbol) != qty)
                        return false;
                    break;
                case (_, _, int min):
                    if (line.Count(symbol.symbol) < min)
                        return false;
                    break;
            }
        return true;
    }
}

class Nerdle : Nerdle<char>
{
    private readonly bool _isMath = default;
    public override required (char symbol, int? qty, int min)[] Symbols
    {
        get => base.Symbols;
        init
        {
            _isMath = value.Any(static s => s.symbol is '=');
            base.Symbols = value;
        }
    }

    protected override Func<char[], bool> GetValidator(char[][] symbols)
    {
        if (_isMath)
            if (symbols.Any(static a => a.Any(static s => s is '(' or ')' or '²' or '³')))
                return IsValidLineMathComplete;
            else
                return IsValidLineMath;
        return base.GetValidator(symbols);
    }

    bool IsValidLineMath(char[] line)
    => line.AsSpan().IndexOf('=') is var index and >= 3
        && CheckSymbolOnTheRight(line.AsSpan()[(index + 1)..])
        && CheckSymbolOnTheLeft(line.AsSpan()[..(index + 1)])
        && CheckFirstAndLast(line)
        && CheckBeforeAndAfterOperator(line.AsSpan()[..(index + 1)])
        && CheckSymbolQty(line, Symbols);

    bool IsValidLineMathComplete(char[] line)
    => line.AsSpan().IndexOf('=') is var index and >= 3
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

    static bool CheckFirstAndLast(ReadOnlySpan<char> line)
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
