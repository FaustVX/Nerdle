using Spectre.Console;

// format for args = 5 ABCDEFGHIJKLMNOPQRSTUVWXYZ
//     nerdle lenght ^ ^~~~~~~~~~~~~~~~~~~~~~~~~^ all symbols

var slotsLength = int.Parse(args[0]);
var symbols = args[1].ToHashSet();

var table = new Table();

for (var s = 1; s <= slotsLength; s++)
    table.AddColumn(new TableColumn(s.ToString()) { Alignment = Justify.Center });

var candidates = GenerateCandidates(slotsLength, symbols)
    .ToArray();

var firsts = new List<Letter>();

table.AddRow(CreateLetters(candidates, slotsLength, firsts));

static IEnumerable<string> GenerateCandidates(int length, IEnumerable<char> symbols)
{
    if (length <= 1)
        foreach (var symbol in symbols)
            yield return symbol.ToString();
    else
        foreach (var candidate in GenerateCandidates(length - 1, symbols))
            foreach (var symbol in symbols)
                yield return candidate + symbol;
}

do
{
    AnsiConsole.Clear();
    AnsiConsole.Write(table);
    Letter.Current!.ProcessKey(AnsiConsole.Console.Input.ReadKey(intercept: true).GetValueOrDefault().Key);
    if (Letter.Current is {} letter)
        letter.Symbols = Nerdle.GetNextSymbol(Letter.StartWith, candidates);
    if (Letter.Current is null)
    {
        (var row, candidates) = AddRow(firsts, slotsLength, symbols);
        table.AddRow(row);
    }
} while (Letter.Current is not null);

static IEnumerable<Letter> CreateLetters(string[] candidates, int length, IList<Letter> firsts)
{
    (var previous, Letter.Current) = (Letter.Current, null);
    return Enumerable.Repeat(candidates, length).Select((c, i) =>
    {
        previous = new Letter() { Previous = previous, Symbols = candidates.Select(c => c[i]).ToHashSet() };
        if (Letter.Current is null)
            firsts.Add(Letter.Current = previous);
        return previous;
    });
}

static (IEnumerable<Letter> letters, string[] candidates) AddRow(IList<Letter> firsts, int length, ISet<char> symbols)
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

    var candidates = new Nerdle()
    {
        Slot = CreateSlots(slots),
        Symbols = CreateSymbols(symbolsQty),
    }
    .GetAllLines(printMaxCombinatory: false, steps: 0)
    .ToArray();

    var outputLayout = new Layout("Output", new Panel(new Rows(candidates.Select(static n => new Text(n)))) { Header = new("Output") });
    var symbolsGrid = new Table();
    symbolsGrid.AddColumn("Symbol");
    symbolsGrid.AddColumn("Quantity");
    symbolsGrid.AddColumn("Minimum");

    AnsiConsole.Write(new Layout().SplitColumns(outputLayout, new("Symbols", new Panel(symbolsGrid) { Header = new("Symbols") })));
    AnsiConsole.Console.Input.ReadKey(intercept: true);

    return (CreateLetters(candidates, length, firsts), candidates);

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
