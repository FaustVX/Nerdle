// format for args = null "ABC" A "D" null "" -- A 0 0 B -1 0 C 5 0

var delimiter = args.AsSpan().IndexOf("--");
var slots = args.AsSpan(0, delimiter);
var symbols = args.AsSpan(delimiter + 1);

static (char?, char[]?)[] CreateSlots(ReadOnlySpan<string> input)
{
    var size = input.Length / 2;
    var slots = new (char?, char[]?)[size];
    for (var i = 0; i < size; i++)
        slots[i] = (input[i * 2] is "null" ? null : input[i * 2][0], string.IsNullOrWhiteSpace(input[i * 2 + 1]) ? Array.Empty<char>() : input[i * 2 + 1].ToCharArray());
    return slots;
}

static (char c, int qty, int min)[] CreateSymbols(ReadOnlySpan<string> input)
{
    var size = input.Length / 3;
    var symbols = new (char c, int qty, int min)[size];
    for (var i = 0; i < size; i++)
        symbols[i] = (input[i * 3][0], int.Parse(input[i * 3 + 1]), int.Parse(input[i * 3 + 2]));
    return symbols;
}

var nerdle0 = new Nerdle()
{
    Slot = CreateSlots(slots),
    Symbols = CreateSymbols(symbols),
}
.GetAllLines(printMaxCombinatory: true, steps: 200);

#if RELEASE
foreach (var line in nerdle0)
    Console.WriteLine(line);
#endif
