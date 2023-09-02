using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

public sealed record class SymbolsQtyStyles(Style? MinQty, Style? NotPresent, Style? QtyFixed, Style? QtyUnknows);

public sealed partial class Setting
{
    [ModuleInitializer]
    internal static void Load()
    {
        var path = "settings.json";
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                new IReadOnlySetConverter<string>(),
                new IReadOnlySetConverter<int>(),
                new StyleConverter(),
            },
            TypeInfoResolver = Ext.JSONContext.Default,
        };
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

    private sealed class IReadOnlySetConverter<T> : JsonConverter<IReadOnlySet<T>>
    {
        public override HashSet<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException();

            reader.Read();

            var elements = new HashSet<T>();

            while (reader.TokenType != JsonTokenType.EndArray)
            {
                elements.Add(JsonSerializer.Deserialize<T>(ref reader, options)!);
                reader.Read();
            }

            return elements;
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlySet<T> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var symbol in value)
                JsonSerializer.Serialize(writer, symbol, options);
            writer.WriteEndArray();
        }
    }

    private sealed class StyleConverter : JsonConverter<Style>
    {
        public override Style? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException();

            return Style.Parse(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, Style value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value.ToMarkup(), options);
    }
}
