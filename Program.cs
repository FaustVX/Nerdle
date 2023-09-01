using System.Runtime.InteropServices;
using Spectre.Console;
using Optional;
using Optional.Unsafe;

// format for args = // Empty

// format for args = path
//     savegame path ^~~^

// format for args = 5 ABCDEFGHIJKLMNOPQRSTUVWXYZ
//     nerdle length ^ ^~~~~~~~~~~~~~~~~~~~~~~~~^ all symbols

//         all symbols v~~~~~~~~~~~~~~~~~~~~~~~~v
// format for args = 5 ABCDEFGHIJKLMNOPQRSTUVWXYZ path
//     nerdle length ^            dictionary path ^~~^

var (slotsLength, savedGuesses, symbols, probabilityPath) = Load(args);
var probabilities = probabilityPath is not null
    ? WordleProbabilistic.CreateMarkovChain(File.ReadAllLines(probabilityPath).ToHashSet())
    : null;

static (int length, IReadOnlyList<Letter>? guesses, IReadOnlySet<char> validSymbols, string? probabilityPath) Load(string[] args)
{
    if (args is [])
    {
        if (AnsiConsole.Prompt(
            new SelectionPrompt<bool>()
            {
                Title = "Select a model ?",
                Converter = static b => b ? "Yes" : "No",
            }
            .AddChoices(true, false)))
        {
            var path = AnsiConsole.Prompt(
            new SelectionPrompt<FileInfo>()
            {
                Title = "Choose a model",
                MoreChoicesText = "[grey](Move up and down to reveal more files)[/]",
                Converter = static f => f.Directory?.Name == "models"
                ? Path.Combine("models", f.Name)
                : f.Name,
            }
            .If(File.Exists("output.json"), static p => p.AddChoices(new FileInfo("output.json")))
            .AddChoices(new DirectoryInfo("models").EnumerateFiles().Where(static f => f.Extension == ".json")));
            return Ext.Load(path.FullName);
        }
        else
        {
            var length = AnsiConsole.Prompt(
                new SelectionPrompt<int>()
                {
                    Title = "Number of slots",
                }
                .AddChoices(5, 6, 7, 8, 9, 10));
            var symbols = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                {
                    Title = "Choose a symbols list",
                }
                .AddChoices("ABCDEFGHIJKLMNOPQRSTUVWXYZ", "1234567890+-*/=", "1234567890+-*/()²³="))
            .ToHashSet();
            var path = AnsiConsole.Prompt(
                new SelectionPrompt<FileInfo>()
                {
                    Title = "Choose a dictionary",
                    MoreChoicesText = "[grey](Move up and down to reveal more files)[/]",
                    Converter = static f => f.Exists ? f.Name : "None",
                }
                .AddChoices(new FileInfo("_"))
                .AddChoices(new DirectoryInfo("dictionaries").EnumerateFiles().Where(static f => f.Extension == ".txt")));
            Ext.Save(Array.Empty<Letter>(), length, Array.Empty<char[]>(), symbols, path.Exists ? path.FullName : default);
            return (length, default, symbols, path.Exists ? path.FullName : default);
        }
    }
    if (args is [var outputPath])
        return Ext.Load(outputPath);
    else
    {
        var slotsLength = int.Parse(args[0]);
        var symbols = args[1].ToHashSet();
        var probabilityPath = args is [_, _, var path]
            ? path
            : null;
        Ext.Save(Array.Empty<Letter>(), slotsLength, Array.Empty<char[]>(), symbols, probabilityPath);
        return (slotsLength, default, symbols, probabilityPath);
    }
}

var table = new Table();

for (var s = 1; s <= slotsLength; s++)
    table.AddColumn(new TableColumn(s.ToString()) { Alignment = Justify.Center });

var firsts = savedGuesses?.ToList() ?? new();

if (firsts is { Count: >= 1 })
    AddPreviousRows(firsts, slotsLength, symbols, table, probabilities);
else
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
        var (_, candidates, symbolsQty, slots) = AddTask(firsts, symbols, probabilities, ctx);
        try
        {
            Ext.Save((List<Letter>)firsts, length, candidates, symbolsQty, probabilityPath);
            return (symbolsQty, candidates, slots);
        }
        catch (CancelException)
        {
            Ext.Save((List<Letter>)firsts, length, Enumerable.Empty<char[]>(), symbolsQty, probabilityPath);
            return (symbolsQty, (IReadOnlyList<char[]>?)null, slots);
        }
    });

    DisplaySummary(candidates, symbolsQty, table);

    return (CreateLetters(symbols, length, firsts, CreateValidSymbols(valid, symbolsQty, symbols)), candidates?.Count ?? -1);
}

static (IEnumerable<Letter[]> words, IReadOnlyList<char[]> candidates, IReadOnlyDictionary<char, (int? qty, int min)> symbolsQty, IEnumerable<(Option<char>, char[]?)> slots) AddTask(IList<Letter> firsts, IReadOnlySet<char> symbols, float[,]? probabilities, ProgressContext ctx)
{
    var (words, slots, symbolsQty) = SetSymbolsQty(firsts, symbols);

    var (candidatesWithCount, qty) = CreateWordle(probabilities, slots, symbolsQty).GetCandidates();
    var task = ctx.AddTask("Calculating", true, qty);
    var candidates = candidatesWithCount.ReportProgress(qty, 1, qty =>
        {
            task.Value = qty;
            return !(AnsiConsole.Console.Input.IsKeyAvailable() && AnsiConsole.Console.Input.ReadKey(intercept: true) is { Key: ConsoleKey.Escape });
        }).Memorize();
    return (words, candidates, symbolsQty, slots);
}

static IReadOnlySet<char>[] CreateValidSymbols(IEnumerable<(Option<char>, char[]?)> valid, IReadOnlyDictionary<char, (int? qty, int min)> symbolsQty, IReadOnlySet<char> symbols)
    => valid.Select(s => (s switch
    {
        ({ HasValue: true } c, _) => Enumerable.Repeat(c.ValueOrDefault(), 1),
        (_, char[] cs and not [' ']) => symbols.Except(cs),
        _ => symbols,
    }).Except(symbolsQty.Where(static kvp => kvp.Value.qty is 0).Select(static kvp => kvp.Key)).ToHashSet()).ToArray();

static Wordle CreateWordle(float[,]? probabilities, (Option<char>, char[]?)[] slots, IReadOnlyDictionary<char, (int? qty, int min)> symbolsQty)
    => probabilities is null
        ? new Wordle()
        {
            Slots = slots,
            Symbols = [.. symbolsQty.Select(static kvp => (kvp.Key, kvp.Value.qty, kvp.Value.min))],
        }
        : new WordleProbabilistic()
        {
            Slots = slots,
            Symbols = [.. symbolsQty.Select(static kvp => (kvp.Key, kvp.Value.qty, kvp.Value.min))],
            Probabilities = probabilities,
            MinProb = float.Epsilon,
        };

static void DisplaySummary(IReadOnlyList<char[]>? candidates, IReadOnlyDictionary<char, (int? qty, int min)> symbolsQty, Table table)
{
    var height = 0;
    var offset = 0;
    do
    {
        height = AnsiConsole.Console.Profile.Height - 3;
        var pathJson = new TextPath("./output.json").RightJustified();
        var pathTxt = new TextPath("./output.txt").LeftJustified();
        var path = new Layout()
            .SplitColumns(new(pathJson), new(new Markup("")), new(pathTxt));
        var panel = candidates is [] or null
            ? new Panel(path) { Header = new($"Output [red](cancelled)[/]"), Expand = true }
            : new Panel(new Rows(path, new Rows(candidates.Skip(offset).Take(height).Select(static n => new Text(new(n)))))) { Header = new($"Output ({offset} / {candidates.Count})"), Expand = true };
        var outputLayout = new Layout("Output", panel);
        var symbolsGrid = new Table() { Expand = true };
        symbolsGrid.AddColumn("Symbol");
        symbolsGrid.AddColumn("Quantity");
        symbolsGrid.AddColumn("Minimum");
        symbolsQty
            .OrderBy(static s => s.Value switch
            {
                (null, not 0) => 1,
                (0, _) => 3,
                (> 0, _) => 0,
                _ => 2,
            })
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

static void AddPreviousRows(IList<Letter> firsts, int length, IReadOnlySet<char> symbols, Table table, float[,]? probabilities)
{
    var (symbolsQty, valid, candidates) = AnsiConsole.Progress()
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
        var (words, candidates, symbolsQty, slots) = AddTask(firsts, symbols, probabilities, ctx);

        foreach (var word in words)
            table.AddRow(word);
        try
        {
            File.WriteAllLines("output.txt", candidates.Select(static l => new string(l)));
            return (symbolsQty, slots, candidates);
        }
        catch (CancelException)
        {
            return (symbolsQty, slots, (IReadOnlyList<char[]>?)null);
        }
    });

    DisplaySummary(candidates, symbolsQty, table);

    table.AddRow(CreateLetters(symbols, length, firsts, CreateValidSymbols(valid, symbolsQty, symbols)));
}

static (Letter[][] words, (Option<char>, char[]?)[] slots, Dictionary<char, (int? qty, int min)> symbolsQty) SetSymbolsQty(IEnumerable<Letter> firsts, IReadOnlySet<char> symbols)
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
    return (words, slots, symbolsQty);
}
