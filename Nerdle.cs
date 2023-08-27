using Optional;
using Optional.Unsafe;

class Nerdle<TSymbol>
where TSymbol : IEquatable<TSymbol>
{
    public required (Option<TSymbol> mandatory, TSymbol[]? forbiden)[] Slots { get; init; }
    protected readonly (TSymbol symbol, int? qty, int min)[] _symbols = default!;
    public (TSymbol symbol, int? qty, int min)[] InitialSymbols { get; private init; } = default!;
    public virtual required (TSymbol symbol, int? qty, int min)[] Symbols
    {
        get => _symbols;
        init => (InitialSymbols, _symbols) = (value, value
            .Where(static s => s is (_, > 0, _) or (_, _, >= 0))
            .Where(s => s.qty is > 0 or null)
            .Where(s => s.qty is null || (Slots.Count(slot => slot.mandatory.Contains(s.symbol)) is var qty && qty != s.qty))
            .ToArray());
    }

    public (IEnumerable<(TSymbol[] value, long count)> candidates, long maxQuantity) GetCandidates()
    {
        // System.Diagnostics.Debugger.Launch();
        var symbols = Enumerable.Repeat((Slots, Symbols), Slots.Length)
            .Select(static (p, pos) => p.Slots[pos] switch
            {
                ({ HasValue: true } m, _) => [m.ValueOrDefault()],
                (_, TSymbol[] f) => p.Symbols.Select(static s => s.symbol).Except(f),
                _ => throw new System.Diagnostics.UnreachableException(),
            })
            .Select(Enumerable.ToArray)
            .ToArray();

        var combinatory = symbols.Aggregate(1L, static (acc, s) => checked(acc *= s.Length));

        return (Process(0, new TSymbol[Slots.Length], symbols).WhereWithCount(GetValidator(symbols)), combinatory);
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
                yield return [.. line];
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
