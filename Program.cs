using System.Runtime.InteropServices;
using Spectre.Console;
using Optional;
using Optional.Unsafe;

// format for args = 5 ABCDEFGHIJKLMNOPQRSTUVWXYZ
//     nerdle length ^ ^~~~~~~~~~~~~~~~~~~~~~~~~^ all symbols

var (slotsLength, _, savedGuesses, symbols, probabilityPath) = Load(args);
var probabilities = probabilityPath is not null
    ? WordleProbalistic.CreateMarkovChain(File.ReadAllLines(probabilityPath).ToHashSet())
    : null;

static (int length, IReadOnlyDictionary<char, (int? qty, int min)>? symbols, IReadOnlyList<Letter>? guesses, IReadOnlySet<char> validSymbols, string? probabilityPath) Load(string[] args)
{
    if (args is [])
    {
        var (slotsLength, symbols, guesses, validSymbols, probabilityPath) = Ext.Load();
        return (slotsLength, symbols, guesses, validSymbols, probabilityPath);
    }
    else
    {
        var slotsLength = int.Parse(args[0]);
        var symbols = args[1].ToHashSet();
        var probabilityPath = args is [_, _, var path]
            ? path
            : null;
        return (slotsLength, default, default, symbols, probabilityPath);
    }
}

var table = new Table();

for (var s = 1; s <= slotsLength; s++)
    table.AddColumn(new TableColumn(s.ToString()) { Alignment = Justify.Center });

var firsts = savedGuesses?.ToList() ?? new();

table.AddRow(CreateLetters(symbols, slotsLength, firsts, Enumerable.Repeat(symbols, slotsLength).ToArray()));

AnsiConsole.Clear();
AnsiConsole.Write(table);
do
{
    var letterChanged = Letter.Current!.ProcessKey(AnsiConsole.Console.Input.ReadKey(intercept: true).GetValueOrDefault());
    if (letterChanged is ProcessKeyReturn.ResetWord)
        Letter.Current = firsts[^1];
    else if (letterChanged is ProcessKeyReturn.NextLetter && Letter.Current is null)
        if (AddRow(firsts, slotsLength, symbols, probabilities, table, probabilityPath) is (var row, not 0))
            table.AddRow(row);
    if (letterChanged is not ProcessKeyReturn.NothingHappened)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(table);
    }
} while (Letter.Current is not null);
AnsiConsole.Clear();
AnsiConsole.Write(table);

static IEnumerable<Letter> CreateLetters(IReadOnlySet<char> symbols, int length, IList<Letter> firsts, IReadOnlySet<char>[] valid)
{
    (var previous, Letter.Current) = (Letter.Current, null);
    return Enumerable.Repeat(symbols, length).Select((s, i) =>
    {
        previous = new Letter() { Previous = previous, Symbols = s, ValidSymbols = valid[i] };
        if (Letter.Current is null)
            firsts.Add(Letter.Current = previous);
        return previous;
    });
}

static (IEnumerable<Letter> letters, int candidates) AddRow(IList<Letter> firsts, int length, IReadOnlySet<char> symbols, float[,]? probabilities, Table table, string? probabilityPath)
{
    var (symbolsQty, candidates, valid) = AnsiConsole.Progress()
    .Columns(
    [
        new SpinnerColumn(Spinner.Known.Aesthetic),
        new TaskDescriptionColumn(),
        new ProgressBarColumn(),
        new PercentageColumn(),
        new ProcessingSpeedColumn(),
        new ElapsedTimeColumn(),
        new RemainingTimeColumn(),
    ])
    .Start(ctx =>
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
                    return (c.Some(), Ext.Space);
                return (Option.None<char>(), column.Where(static l => l.LetterMode != LetterMode.CorrectPlace).Select(static l => l.Selected).ToArray());
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

        var (candidatesWithCount, qty) = probabilities is null
        ? new Wordle()
            {
                Slot = slots,
                Symbols = [.. symbolsQty.Select(static kvp => (kvp.Key, kvp.Value.qty, kvp.Value.min))],
            }.GetAllLines()
        : new WordleProbalistic()
            {
                Slot = slots,
                Symbols = [.. symbolsQty.Select(static kvp => (kvp.Key, kvp.Value.qty, kvp.Value.min))],
                Probalities = probabilities,
                MinProb = float.Epsilon,
            }.GetAllLines();
        var task = ctx.AddTask("Calculating", true, qty);
        var candidates = candidatesWithCount.ReportProgress(qty, 1, qty =>
            {
                task.Value = qty;
                return !(AnsiConsole.Console.Input.IsKeyAvailable() && AnsiConsole.Console.Input.ReadKey(intercept: true) is { Key: ConsoleKey.Escape });
            }).Memorize();
        try
        {
            Ext.Save((List<Letter>)firsts, length, candidates, symbolsQty, probabilityPath);
            return (symbolsQty, (IReadOnlyList<char[]>)candidates, slots);
        }
        catch (CancelException)
        {
            Ext.Save((List<Letter>)firsts, length, Enumerable.Empty<char[]>(), symbolsQty, probabilityPath);
            return (symbolsQty, (IReadOnlyList<char[]>?)null, slots);
        }
    });

    DisplaySummary(candidates, symbolsQty, table);

    return (CreateLetters(symbols, length, firsts, valid.Select(s => (s switch
    {
        ({ HasValue: true } c, _) => Enumerable.Repeat(c.ValueOrDefault(), 1),
        (_, char[] cs and not [' ']) => symbols.Except(cs),
        _ => symbols,
    }).Except(symbolsQty.Where(static kvp => kvp.Value.qty is 0).Select(static kvp => kvp.Key)).ToHashSet()).ToArray()), candidates?.Count ?? -1);
}

static void DisplaySummary(IReadOnlyList<char[]>? candidates, IReadOnlyDictionary<char, (int? qty, int min)> symbolsQty, Table table)
{

    var height = 0;
    var offset = 0;
    do
    {
        height = AnsiConsole.Console.Profile.Height - 3;
        var path = new TextPath("./output.json").Centered();
        var panel = candidates is [] or null
            ? new Panel(path) { Header = new($"Output [red](cancelled)[/]"), Expand = true }
            : new Panel(new Rows(path, new Rows(candidates.Skip(offset).Take(height).Select(static n => new Text(new(n)))))) { Header = new($"Output ({offset} / {candidates.Count})"), Expand = true };
        var outputLayout = new Layout("Output", panel);
        var symbolsGrid = new Table() { Expand = true };
        symbolsGrid.AddColumn("Symbol");
        symbolsGrid.AddColumn("Quantity");
        symbolsGrid.AddColumn("Minimum");
        symbolsQty
            .Select(GenerateRow)
            .Execute(symbolsGrid.AddRow);

        AnsiConsole.Clear();
        table.Expand = true;
        Letter.RenderDecoration = false;
        AnsiConsole.Write(new Layout().SplitColumns(outputLayout, new("Symbols", new Panel(symbolsGrid) { Header = new($"Symbols ({symbolsGrid.Rows.Count})"), Expand = true }), new(new Panel(table) { Header = new($"Guesses ({table.Rows.Count})"), Expand = true })));
        Letter.RenderDecoration = true;
        table.Expand = false;

        static IEnumerable<Markup> GenerateRow(KeyValuePair<char, (int? qty, int min)> kvp)
        {
            yield return new(kvp.Key.ToString(), kvp.Value switch
            {
                (null, not 0) => new(Color.Yellow),
                (0, _) => new(Color.Red),
                (> 0, _) => new(Color.Green),
                _ => null,
            });
            yield return new(kvp.Value.qty?.ToString() ?? "?");
            yield return new(kvp.Value.min.ToString());
        }
    } while (ProcessKey(AnsiConsole.Console.Input.ReadKey(intercept: true).GetValueOrDefault().Key, ref offset, (candidates?.Count ?? 0) - height, height - 1));

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
                if (offset < 0)
                    offset = 0;
                return true;
            default:
                return true;
        }
    }
}
