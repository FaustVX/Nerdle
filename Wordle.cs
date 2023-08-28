class Wordle : Nerdle<char>
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
        return !(a > 0 || s > 0 || m != -1 || d != -1 || e != -1 || l != -1 || r != -1 || q != -1 || c != -1);
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
    => line[0] is >= '0' and <= '9' or '(' or '-' or '+' && line[^1] is >= '0' and <= '9';

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
