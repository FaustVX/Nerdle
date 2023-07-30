using Spectre.Console;

// format for args = 5 ABCDEFGHIJKLMNOPQRSTUVWXYZ

var slotsLength = int.Parse(args[0]);
var symbols = args[1];

var table = new Table();
for (var s = 1; s <= slotsLength; s++)
    table.AddColumn(new TableColumn(s.ToString()) { Alignment = Justify.Center });
var previous = Letter.Current;
table.AddRow(Enumerable.Range(0, slotsLength).Select(_ =>
{
    previous = new Letter(symbols.ToHashSet()) { Previous = previous };
    Letter.Current ??= previous;
    return previous;
}));

do
{
    AnsiConsole.Clear();
    AnsiConsole.Write(table);
    Letter.Current!.ProcessKey(AnsiConsole.Console.Input.ReadKey(intercept: true).GetValueOrDefault().Key);
} while (Letter.Current is not null);
