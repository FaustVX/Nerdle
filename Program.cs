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

AnsiConsole.Clear();
AnsiConsole.Write(table);
do
{
    var letterChanged = Letter.Current!.ProcessKey(AnsiConsole.Console.Input.ReadKey(intercept: true).GetValueOrDefault());
    if (Letter.Current is {} letter)
    {
        if (letterChanged is ProcessKeyReturn.NextLetter)
            letter.Symbols = Nerdle.GetNextSymbol(Letter.StartWith, candidates);
    }
    else
    {
        (var row, candidates) = AddRow(firsts, slotsLength, symbols);
        if (candidates.Length > 1)
            table.AddRow(row);
    }
    if (letterChanged is not ProcessKeyReturn.NothingHappened)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(table);
    }
} while (Letter.Current is not null);
AnsiConsole.Clear();
AnsiConsole.Write(table);

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
        .Select(static l => l.SelectAll(static l => l.Next!, static l => l != null).ToArray())
        .ToArray();
    var columns = words
        .Transpose()
        .Select(static l => l.ToArray())
        .ToArray();
    var slots = columns
        .SelectMany(static column =>
        {
            if (column.FirstOrDefault(static l => l.LetterMode == LetterMode.CorrectPlace) is { Selected: var c })
                return new string[2] { c.ToString(), " " };
            return new string[2] { "null", string.Concat(column.Where(static l => l.LetterMode != LetterMode.CorrectPlace).Select(static l => l.Selected)) };
        })
        .ToArray();
    var symbolsQty = symbols
        .Select(static s => (s, new int?(), 0))
        .ToArray();

    var candidates = new Nerdle()
    {
        Slot = CreateSlots(slots),
        Symbols = symbolsQty,
    }
    .GetAllLines(printMaxCombinatory: false, steps: 0)
    .ToArray();

    var outputLayout = new Layout("Output", new Panel(new Rows(candidates.Select(static n => new Text(n)))) { Header = new("Output"), Expand = true });
    var symbolsGrid = new Table() { Expand = true };
    symbolsGrid.AddColumn("Symbol");
    symbolsGrid.AddColumn("Quantity");
    symbolsGrid.AddColumn("Minimum");
    foreach (var (c, qty, min) in symbolsQty)
        symbolsGrid.AddRow(c.ToString(), qty?.ToString() ?? "?", min.ToString());

    AnsiConsole.Write(new Layout().SplitColumns(outputLayout, new("Symbols", new Panel(symbolsGrid) { Header = new("Symbols"), Expand = true })));
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
}
