using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

sealed record class SymbolsQtyStyles(Style? MinQty, Style? NotPresent, Style? QtyFixed, Style? QtyUnknows);
sealed record class Theme(SymbolsQtyStyles SymbolsQtyStyles, FrozenDictionary<LetterMode, Style> LetterModeStyle, Style LetterSelectedStyle)
{
    public static Theme Default()
        => new(new(new(Color.Yellow), new(Color.Red), new(Color.Green), null),
            new Dictionary<LetterMode, Style>()
            {
                [LetterMode.Unknown] = Style.Plain,
                [LetterMode.CorrectPlace] = new(Color.Green),
                [LetterMode.InvalidePlace] = new(Color.Yellow),
                [LetterMode.InvalideLetter] = new(Color.Grey, Color.Black),
            }.ToFrozenDictionary(),
            new(decoration: Decoration.Italic | Decoration.Underline));
}

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
    public required FrozenDictionary<string, Theme> Themes { get; init; } = new Dictionary<string, Theme>()
    {
        ["Default"] = Theme.Default(),
    }.ToFrozenDictionary();

    [JsonIgnore]
    public Theme Theme { get; set; } = default!;

    public string SelectedTheme
    {
        init => Theme = Themes.TryGetValue(value ?? "", out var theme) ? theme : Theme;
    }

    [JsonIgnore]
    public SymbolsQtyStyles SymbolsQtyStyles => Theme.SymbolsQtyStyles;

    [JsonIgnore]
    public FrozenDictionary<LetterMode, Style> LetterModeStyle => Theme.LetterModeStyle;

    [JsonIgnore]
    public Style LetterSelectedStyle => Theme.LetterSelectedStyle;
}
