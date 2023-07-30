using Spectre.Console;

// format for args = 5 ABCDEFGHIJKLMNOPQRSTUVWXYZ

var slotsLength = int.Parse(args[0]);
var symbols = args[1].ToHashSet();

var table = new Table();

for (var s = 1; s <= slotsLength; s++)
    table.AddColumn(new TableColumn(s.ToString()) { Alignment = Justify.Center });

var previous = Letter.Current;
table.AddRow(Enumerable.Range(0, slotsLength).Select(_ =>
{
    previous = new Letter() { Previous = previous, Symbols = symbols };
    Letter.Current ??= previous;
    return previous;
}));

var firsts = new List<Letter>()
{
    Letter.Current!,
};

#if DEBUG
    Letter.Current!.Selected = Random.Shared.GetItem(args[1].ToCharArray());
    Letter.Current!.LetterMode = Random.Shared.GetItem<LetterMode>();
    Letter.Current.Next!.Selected = Random.Shared.GetItem(args[1].ToCharArray());
    Letter.Current.Next!.LetterMode = Random.Shared.GetItem<LetterMode>();
    Letter.Current.Next.Next!.Selected = Random.Shared.GetItem(args[1].ToCharArray());
    Letter.Current.Next.Next!.LetterMode = Random.Shared.GetItem<LetterMode>();
    Letter.Current.Next.Next.Next!.Selected = Random.Shared.GetItem(args[1].ToCharArray());
    Letter.Current.Next.Next.Next!.LetterMode = Random.Shared.GetItem<LetterMode>();
    Letter.Current.Next.Next.Next.Next!.Selected = Random.Shared.GetItem(args[1].ToCharArray());
    Letter.Current.Next.Next.Next.Next!.LetterMode = Random.Shared.GetItem<LetterMode>();
    Letter.Current = previous;
#endif

do
{
    AnsiConsole.Clear();
    AnsiConsole.Write(table);
    Letter.Current!.ProcessKey(AnsiConsole.Console.Input.ReadKey(intercept: true).GetValueOrDefault().Key);
    if (Letter.Current is null)
        table.AddRow(AddRow(firsts, slotsLength, symbols));
} while (Letter.Current is not null);

static IEnumerable<Letter> AddRow(IList<Letter> firsts, int length, ISet<char> symbols)
{
    var words = firsts
        .Select(static l => l.SelectAll(static l => l.Next!, static l => l != null))
        .ToArray();
    var letters = words
        .Transpose()
        .Select(static l => l.ToArray())
        .ToArray();
    var slots = letters
        .SelectMany(static l => l
            .SelectMany(static l => l switch
            {
                { LetterMode: LetterMode.CorrectPlace, Selected: var c } => new string[2] { c.ToString(), " " },
                { Selected: var c } => new string[2] { "null", c.ToString() }
            }))
        .ToArray();
    var symbolsQty = symbols
        .SelectMany(static s => new string[3]
            {
                s.ToString(),
                "0",
                "0",
            })
        .ToArray();

    var nerdle = new Nerdle()
    {
        Slot = CreateSlots(slots),
        Symbols = CreateSymbols(symbolsQty),
    }
    .GetAllLines(printMaxCombinatory: false, steps: 0);

    var outputLayout = new Layout("Output", new Panel(new Rows(nerdle.Select(static n => new Text(n)))) { Header = new("Output") });
    var symbolsGrid = new Table();
    symbolsGrid.AddColumn("Symbol");
    symbolsGrid.AddColumn("Quantity");
    symbolsGrid.AddColumn("Minimum");

    AnsiConsole.Write(new Layout().SplitColumns(outputLayout, new("Symbols", new Panel(symbolsGrid) { Header = new("Symbols") })));
    AnsiConsole.Console.Input.ReadKey(intercept: true);

    yield break;

    static (char?, char[]?)[] CreateSlots(IReadOnlyList<string> input)
    {
        var size = input.Count / 2;
        var slots = new (char?, char[]?)[size];
        for (var i = 0; i < size; i++)
            slots[i] = (input[i * 2] is "null" ? null : input[i * 2][0], string.IsNullOrWhiteSpace(input[i * 2 + 1]) ? Array.Empty<char>() : input[i * 2 + 1].ToCharArray());
        return slots;
    }

    static (char c, int qty, int min)[] CreateSymbols(IReadOnlyList<string> input)
    {
        var size = input.Count / 3;
        var symbols = new (char c, int qty, int min)[size];
        for (var i = 0; i < size; i++)
            symbols[i] = (input[i * 3][0], int.Parse(input[i * 3 + 1]), int.Parse(input[i * 3 + 2]));
        return symbols;
    }
}
