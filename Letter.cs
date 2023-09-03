using Spectre.Console;
using Spectre.Console.Rendering;

partial class Letter: IRenderable
{
    public static Letter? Current { get; set; } = default!;
    public static string StartWith { get; private set; } = "";

    private int _selected;
    private char[] _symbols = default!;

    private int SymbolsLength => _symbols.Length;
    private bool IsLetterSelected => this == Current;
    public char Selected
    {
        get => _symbols[_selected];
        set => _selected = _symbols.AsSpan().IndexOf(value) switch
        {
            < 0 => 0,
            var i => i,
        };
    }

    private readonly IReadOnlySet<char> validSymbols = default!;
    public required IReadOnlySet<char> ValidSymbols
    {
        get => validSymbols;
        init
        {
            validSymbols = value;
            if (ValidSymbols.Count == 1)
                (Selected, LetterMode) = (ValidSymbols.First(), LetterMode.CorrectPlace);
        }
    }

    public LetterMode LetterMode { get; set; }
    public required Letter? Previous
    {
        init
        {
            if (value is Letter l)
                l.Next = this;
        }
    }

    public required IReadOnlySet<char>? Symbols
    {
        set
        {
            if (_symbols is null)
            {
                _symbols = value?.ToArray() ?? [];
                if (SymbolsLength <= 1)
                    LetterMode = LetterMode.CorrectPlace;
                return;
            }
            var selected = Selected;
            _symbols = value?.ToArray() ?? [];
            (_selected, LetterMode) = _symbols.AsSpan().IndexOf(selected) is >= 0 and var i ? (i, LetterMode) : (0, LetterMode.Unknown);
            if (SymbolsLength <= 1)
                LetterMode = LetterMode.CorrectPlace;
        }
    }

    public Letter? Next { get; private set; }

    public static bool RenderDecoration { get; set; } = true;

    Measurement IRenderable.Measure(RenderOptions options, int maxWidth)
    => new(1, 1);

    IEnumerable<Segment> IRenderable.Render(RenderOptions options, int maxWidth)
    {
        var style = IsLetterSelected ? Setting.Instance.LetterSelectedStyle : Style.Plain;
        style = style.Combine(Setting.Instance.LetterModeStyle[LetterMode]);
        if (!ValidSymbols.Contains(Selected))
            style = style.Decoration(style.Decoration | Decoration.Strikethrough);
        yield return new(Selected.ToString(), style.If(!RenderDecoration, static s => s.Decoration(Decoration.None)));
    }

    public ProcessKeyReturn ProcessKey(ConsoleKeyInfo key)
    {
        switch (key)
        {
            case { Key: ConsoleKey.Backspace }:
                StartWith = "";
                return ProcessKeyReturn.ResetWord;
            case { Key: ConsoleKey.UpArrow }:
                if (SymbolsLength <= 1)
                    return ProcessKeyReturn.NothingHappened;
                if (_selected <= 0)
                    _selected = SymbolsLength;
                _selected--;
                return ProcessKeyReturn.Updated;
            case { Key: ConsoleKey.DownArrow }:
                if (SymbolsLength <= 1)
                    return ProcessKeyReturn.NothingHappened;
                _selected++;
                if (_selected >= SymbolsLength)
                    _selected = 0;
                return ProcessKeyReturn.Updated;
            case { Key: ConsoleKey.Spacebar }:
                LetterMode++;
                if ((int)LetterMode > 3)
                    LetterMode = LetterMode.CorrectPlace;
                return ProcessKeyReturn.Updated;
            case { Key: ConsoleKey.Enter } when LetterMode != LetterMode.Unknown:
                StartWith = Current?.Next is null ? "" : (StartWith + Selected);
                Current = Next;
                return ProcessKeyReturn.NextLetter;
            case { KeyChar: var c } when _symbols.Contains(c):
                Selected = c;
                return ProcessKeyReturn.Updated;
            case { KeyChar: var c } when _symbols.Contains(char.ToUpperInvariant(c)):
                Selected = char.ToUpperInvariant(c);
                return ProcessKeyReturn.Updated;
            default:
                return ProcessKeyReturn.NothingHappened;
        }
    }
}

enum LetterMode
{
    Unknown = 0,
    CorrectPlace,
    InvalidePlace,
    InvalideLetter,
}

public enum ProcessKeyReturn
{
    NothingHappened = 0,
    NextLetter,
    Updated,
    ResetWord,
}
