using System.Runtime.InteropServices;
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
    if (letterChanged is ProcessKeyReturn.ResetWord)
    {
        Letter.Current = firsts[^1];
        continue;
    }
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
        .Select(static column =>
        {
            if (column.FirstOrDefault(static l => l.LetterMode == LetterMode.CorrectPlace) is { Selected: var c })
                return (new char?(c), Ext.Space);
            return (new char?(), column.Where(static l => l.LetterMode != LetterMode.CorrectPlace).Select(static l => l.Selected).ToArray());
        })
        .ToArray();
    var symbolsQty = symbols
        .ToDictionary(static s => s, static s => (qty: new int?(), min: 0));
    foreach (var word in words)
        foreach (var (c, letters) in word.GroupBy(static l => l.Selected).ToDictionary(static g => g.Key, static g => g.OrderBy(static l => l.LetterMode).ToArray()))
            if (letters[0].LetterMode is LetterMode.InvalideLetter)
                CollectionsMarshal.GetValueRefOrNullRef(symbolsQty, c).qty = 0;
            else if (letters[^1].LetterMode is LetterMode.InvalideLetter)
                CollectionsMarshal.GetValueRefOrNullRef(symbolsQty, c).qty = letters.Count(static l => l.LetterMode is not LetterMode.InvalideLetter);
            else
            {
                ref var symbol = ref CollectionsMarshal.GetValueRefOrNullRef(symbolsQty, c);
                symbol.min = Math.Max(letters.Length, symbol.min);
            }

    var candidates = new Nerdle()
    {
        Slot = slots,
        Symbols = symbolsQty.Select(static kvp => (kvp.Key, kvp.Value.qty, kvp.Value.min)).ToArray(),
    }
    .GetAllLines(printMaxCombinatory: false, steps: 0)
    .ToArray();

    File.WriteAllLines("output.txt", candidates);

    var height = 0;
    var offset = 0;
    do
    {
        height = AnsiConsole.Console.Profile.Height - 3;
        var outputLayout = new Layout("Output", new Panel(new Rows(candidates.Skip(offset).Take(height).Select(static n => new Text(n)))) { Header = new($"Output ({candidates.Length})"), Expand = true });
        var symbolsGrid = new Table() { Expand = true };
        symbolsGrid.AddColumn("Symbol");
        symbolsGrid.AddColumn("Quantity");
        symbolsGrid.AddColumn("Minimum");
        foreach (var (c, (qty, min)) in symbolsQty)
            symbolsGrid.AddRow(c.ToString(), qty?.ToString() ?? "?", min.ToString());

        AnsiConsole.Clear();
        AnsiConsole.Write(new Layout().SplitColumns(outputLayout, new("Symbols", new Panel(symbolsGrid) { Header = new($"Symbols ({symbolsGrid.Rows.Count})"), Expand = true })));
    } while (ProcessKey(AnsiConsole.Console.Input.ReadKey(intercept: true).GetValueOrDefault().Key, ref offset, candidates.Length - height, height - 1));

    return (CreateLetters(candidates, length, firsts), candidates);

    static bool ProcessKey(ConsoleKey key, ref int offset, int length, int move)
    {
        if (length < 0)
            length = 0;
        if (move < 1)
            move = 1;
        switch (key)
        {
            case ConsoleKey.Enter or ConsoleKey.Escape:
                return false;
            case ConsoleKey.UpArrow:
                offset -= move;
                if (offset <= 0)
                    offset = 0;
                return true;
            case ConsoleKey.DownArrow:
                offset += move;
                if (offset >= length)
                    offset = length - 1;
                return true;
            default:
                return true;
        }
    }
}
