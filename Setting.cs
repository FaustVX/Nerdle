using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

sealed record class SymbolsQtyStyles(Style? MinQty, Style? NotPresent, Style? QtyFixed, Style? QtyUnknows);

sealed partial class Setting
{
    [ModuleInitializer]
    internal static void Load()
    {
        var path = "settings.json";
        var options = Ext.JSONContext.GetOptions();
        try
        {
            using var stream = File.OpenRead(path);
            Instance = (Setting)JsonSerializer.Deserialize(stream, typeof(Setting), options)!;
        }
        catch
        {
            var json = JsonSerializer.Serialize(Instance = new(), typeof(Setting), options);
            File.WriteAllText(path, json);
        }
    }

    [SetsRequiredMembers]
    public Setting()
    { }

    public static Setting Instance { get; private set; } = default!;

    [JsonRequired]
    public required IReadOnlySet<string> Symbols { get; init; } =
    (HashSet<string>)[
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
        "1234567890+-*/=",
        "1234567890+-*/()²³=",
    ];

    [JsonRequired]
    public required IReadOnlySet<int> Slots { get; init; } = (HashSet<int>)[ 5, 6, 7, 8, 9, 10 ];

    [JsonRequired]
    public required bool SortSymbols { get; init; } = true;

    [JsonRequired]
    public required SymbolsQtyStyles SymbolsQtyStyles { get; init; } = new(new(Color.Yellow), new(Color.Red), new(Color.Green), null);

    [JsonRequired]
    public required FrozenDictionary<LetterMode, Style> LetterModeStyle { get; init; } = new Dictionary<LetterMode, Style>()
    {
        [LetterMode.Unknown] = Style.Plain,
        [LetterMode.CorrectPlace] = new(Color.Green),
        [LetterMode.InvalidePlace] = new(Color.Yellow),
        [LetterMode.InvalideLetter] = new(Color.Grey, Color.Black),
    }.ToFrozenDictionary();

    [JsonRequired]
    public required Style LetterSelectedStyle { get; init; } = new(decoration: Decoration.Italic | Decoration.Underline);
}
