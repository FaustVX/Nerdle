using Spectre.Console;
using Spectre.Console.Rendering;
using System.Diagnostics;

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
                < 0 => throw new ArgumentOutOfRangeException(nameof(Selected), value.ToString(), null),
                var i => i,
            };
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

    public required ISet<char>? Symbols
    {
        set
        {
            if (_symbols is null)
            {
                _symbols = value?.ToArray() ?? Array.Empty<char>();
                return;
            }
            var selected = Selected;
            _symbols = value?.ToArray() ?? Array.Empty<char>();
            (_selected, LetterMode) = _symbols.AsSpan().IndexOf(selected) is >= 0 and var i ? (i, LetterMode) : (0, LetterMode.Unknown);
        }
    }

    public Letter? Next { get; private set; }

    Measurement IRenderable.Measure(RenderOptions options, int maxWidth)
    => new(1, 1);

    IEnumerable<Segment> IRenderable.Render(RenderOptions options, int maxWidth)
    {
        var (background, foreground) = LetterMode switch
        {
            LetterMode.Unknown => (Color.Default, Color.Default),
            LetterMode.CorrectPlace => (Color.Green, Color.Default),
            LetterMode.InvalidePlace => (Color.Yellow, Color.Default),
            LetterMode.InvalideLetter => (Color.Grey, Color.Black),
            _ => throw new UnreachableException(),
        };
        yield return new(Selected.ToString(), new(background: background, foreground: foreground, decoration: IsLetterSelected ? Decoration.Underline : null));
    }

    public void ProcessKey(ConsoleKey key)
    {
        switch (key)
        {
            case ConsoleKey.UpArrow:
                if (_selected <= 0)
                    _selected = SymbolsLength;
                _selected--;
                break;
            case ConsoleKey.DownArrow:
                _selected++;
                if (_selected >= SymbolsLength)
                    _selected = 0;
                break;
            case ConsoleKey.Spacebar:
                LetterMode++;
                if ((int)LetterMode > 3)
                    LetterMode = LetterMode.Unknown;
                break;
            case ConsoleKey.Enter when LetterMode != LetterMode.Unknown:
                StartWith = Current?.Next is null ? "" : (StartWith + Selected);
                Current = Next;
                break;
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
