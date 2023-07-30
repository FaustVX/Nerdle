using Spectre.Console;
using PrimaryParameter.SG;
using Spectre.Console.Rendering;
using System.Diagnostics;

partial class Letter([Field(Type = typeof(char[]), AssignFormat = "{0}.ToArray()")] ISet<char> symbols) : IRenderable
{
    public static Letter? Current { get; set; } = default!;

    private int _selected;

    private int SymbolsLength => _symbols.Length;
    private bool IsLetterSelected => this == Current;
    public char Selected => _symbols[_selected];
    public LetterMode LetterMode { get; set; }
    public required Letter? Previous
    {
        init
        {
            if (value is Letter l)
                l.Next = this;
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
            case ConsoleKey.Enter:
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
