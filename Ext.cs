using System.Collections.Frozen;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

public class CancelException : Exception
{ }

[DebuggerStepThrough]
public static partial class Ext
{
    public static Func<TIn, TResult> Then<TIn, TOut, TResult>(this Func<TIn, TOut> func, Func<TIn, TOut, TResult> then)
    => i => then(i, func(i));

    public static T If<T>(this T obj, bool condition, Func<T, T> @true, Func<T, T>? @false = null)
        => condition
        ? @true(obj)
        : @false is null ? obj : @false(obj);

    public static IEnumerable<T> ReportProgress<T>(this IEnumerable<(T value, long count)> items, long max, int steps, Func<long, bool> progress)
    {
        if (steps <= 0 || steps >= max)
            return items.Select(static item => item.value);
        return Report(items, max, steps, progress);

        static IEnumerable<T> Report(IEnumerable<(T value, long count)> items, long max, int steps, Func<long, bool> progress)
        {
            using var enumerator = items.GetEnumerator();

            for (var i = 0L; enumerator.MoveNext(); i++)
            {
                if (i % steps == 0 && !progress(enumerator.Current.count))
                    throw new CancelException();
                yield return enumerator.Current.value;
            }
            progress(max);
        }
    }

    public static T Forward<T>(this T value, Func<T, T> func, int count)
    {
        if (count <= 0)
            return value;
        return func(value).Forward(func, count - 1);
    }

    public static IEnumerable<T> SelectAll<T>(this T value, Func<T, T> selector, Func<T, bool> @while)
    {
        if (!@while(value))
            yield break;
        yield return value;
        foreach (var item in SelectAll(selector(value), selector, @while))
            yield return item;
    }

    public static IEnumerable<IEnumerable<T>> Transpose<T>(this IEnumerable<IEnumerable<T>> array)
    {
        var a = array.Select(static x => x.ToArray()).ToArray();

        for (var y = 0; y < a[0].Length; y++)
            yield return Selector(a, y);

        static IEnumerable<T> Selector(T[][] a, int y)
        {
            for (var x = 0; x < a.Length; x++)
                yield return a[x][y];
        }
    }

    public static T GetItem<T>(this Random random, IList<T> values)
    => values[random.Next(values.Count)];

    public static TEnum GetItem<TEnum>(this Random random)
    where TEnum : struct, Enum
    => random.GetItem(Enum.GetValues<TEnum>());

    public static void Execute<TIn, TOut>(this IEnumerable<TIn> values, Func<TIn, TOut> func)
    {
        foreach (var item in values)
            func(item);
    }

    public static IEnumerable<(T value, long count)> WhereWithCount<T>(this IEnumerable<T> values, Func<T, bool> condition)
    {
        var count = 0L;
        foreach (var item in values)
        {
            count++;
            if (condition(item))
                yield return (item, count);
        }
    }

    public static IEnumerable<(T value, long count)> WhereWithCount<T>(this IEnumerable<(T value, long count)> values, Func<T, bool> condition)
    {
        var count = 0L;
        foreach (var (value, c) in values)
        {
            count += c;
            if (condition(value))
                yield return (value, count);
        }
    }

    public static Memorizer<T> Memorize<T>(this IEnumerable<T> values)
    => new(values.GetEnumerator());

    internal static void Save(IReadOnlyList<Letter> guesses, int length, IEnumerable<char[]> candidates, IReadOnlyDictionary<char, (int? qty, int min)> symbolsQty, string? probabilityDictionary)
        => Save(guesses, length, candidates, symbolsQty.Select(static kvp => kvp.Key).ToHashSet(), probabilityDictionary);

    internal static void Save(IReadOnlyList<Letter> guesses, int length, IEnumerable<char[]> candidates, HashSet<char> symbols, string? probabilityDictionary)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            },
            TypeInfoResolver = JSONContext.Default,
        };
        var g = guesses
            .Select(static g => g
                .SelectAll(static l => l.Next!, static l =>l is not null)
                .Select(static l => new Saving.Guess(l.Selected, l.LetterMode))
                .ToArray())
            .ToList();
        var saving = new Saving(length, probabilityDictionary, symbols, g);
#pragma warning disable IL2026 // Using member 'System.Text.Json.JsonSerializer.Serialize(Stream, Type, JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.
        var jsonString = JsonSerializer.Serialize(saving, typeof(Saving), options);
#pragma warning restore IL2026
        File.WriteAllText("output.json", jsonString);
        if (candidates.Any())
            File.WriteAllLines("output.txt", candidates.Select(static l => new string(l)));
    }

    internal static (int length, IReadOnlyList<Letter> guesses, IReadOnlySet<char> validSymbols, string? probabilityDictionary) Load(string? path = null)
    {
        path ??= "output.json";
        var options = JSONContext.GetOptions();
        using var utf8Json = File.OpenRead(path);
#pragma warning disable IL2026 // Using member 'System.Text.Json.JsonSerializer.Deserialize(Stream, Type, JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.
        var (length, guesses, validSymbols, file, _) = (Saving)JsonSerializer.Deserialize(utf8Json, typeof(Saving), options)!;
#pragma warning restore IL2026
        return (length, guesses, validSymbols, file);
    }

    public static string ToMarkupOk(this Style style)
    {
        var markup = style.ToMarkup();
        if (string.IsNullOrWhiteSpace(markup))
            return "default";
        return markup;
    }

#if !NET8_0_OR_GREATER
    public static int Count<T>(this ReadOnlySpan<T> values, T value)
    {
        var count = 0;
        foreach (var item in values)
            if (item?.Equals(value) ?? false)
                count++;
        return count;
    }
#endif

    public readonly static char[]? Space = [' '];

    internal sealed record class Saving(int Length, string? ProbabilityDictionary, HashSet<char> Symbols, List<Saving.Guess[]> Guesses)
    {
        [JsonPropertyOrder(-1)]
        public int Version
        {
            get => 3;
            init
            {
                if (value != Version)
                    throw new InvalidOperationException($"Incorrect version number. Provided {value}, Expected {Version}");
            }
        }

        public sealed record class Guess(char Value, LetterMode Mode);

        public void Deconstruct(out int length, out IReadOnlyList<Letter> guesses, out IReadOnlySet<char> validSymbols, out string? probabilityDictionary, out object? _)
        {
            length = Length;
            validSymbols = Symbols;
            guesses = Guesses.Select(gs => gs.Select(g => new Letter()
            {
                Previous = null!,
                Symbols = Symbols,
                ValidSymbols = Symbols,
                Selected = g.Value,
                LetterMode = g.Mode,
            }).ToArray())
            .Select(CreateLetter)
            .ToList();
            probabilityDictionary = ProbabilityDictionary;
            _ = default;

            Letter CreateLetter(Letter[] letters)
            {
                var first = letters[0];
                var last = first;
                for (var i = 1; i < letters.Length; i++)
                {
                    var letter = letters[i];
                    last = new()
                    {
                        Previous = last,
                        Symbols = Symbols,
                        ValidSymbols = letter.ValidSymbols,
                        Selected = letter.Selected,
                        LetterMode = letter.LetterMode,
                    };
                }
                return first;
            }
        }
    }

    [JsonSerializable(typeof(Saving)), JsonSerializable(typeof(Setting))]
    [JsonSerializable(typeof(int)), JsonSerializable(typeof(bool)), JsonSerializable(typeof(string)), JsonSerializable(typeof(HashSet<char>)), JsonSerializable(typeof(char)), JsonSerializable(typeof(List<Saving.Guess>)), JsonSerializable(typeof(Saving.Guess))]
    [JsonSerializable(typeof(IReadOnlySet<IReadOnlySet<char>>)), JsonSerializable(typeof(IReadOnlySet<char>)), JsonSerializable(typeof(char)), JsonSerializable(typeof(FrozenDictionary<LetterMode, Style>)), JsonSerializable(typeof(FrozenDictionary<string, Theme>)), JsonSerializable(typeof(Theme))]
    internal partial class JSONContext : JsonSerializerContext
    {
        public static JsonSerializerOptions GetOptions()
        => new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                new IReadOnlySetConverter<string>(),
                new IReadOnlySetConverter<int>(),
                new FrozenDictionaryConverter<string, Theme>(),
                new FrozenDictionaryConverter<LetterMode, Style>(),
                new StyleConverter(),
            },
            TypeInfoResolver = Default,
        };

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

        private sealed class FrozenDictionaryConverter<TKey, TValue> : JsonConverter<FrozenDictionary<TKey, TValue>>
        where TKey : notnull
        {
            public override FrozenDictionary<TKey, TValue>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException();

                reader.Read();

                var elements = new Dictionary<TKey, TValue>();

                while (reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                        throw new JsonException();
                    var property = reader.GetString()!;
                    var key = JsonSerializer.Deserialize<TKey>($"\"{property}\"", options)!;
                    reader.Read();
                    var value = JsonSerializer.Deserialize<TValue>(ref reader, options)!;
                    reader.Read();
                    elements.Add(key, value);
                }

                return elements.ToFrozenDictionary();
            }

            public override void Write(Utf8JsonWriter writer, FrozenDictionary<TKey, TValue> value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                foreach (var (key, val) in value)
                {
                    writer.WritePropertyName(JsonSerializer.Serialize(key, options)[1..^1]);
                    JsonSerializer.Serialize(writer, val, options);
                }
                writer.WriteEndObject();
            }
        }

        private sealed class StyleConverter : JsonConverter<Style>
        {
            public override Style? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                    throw new JsonException();

                var v = reader.GetString();
                if (string.IsNullOrEmpty(v))
                    return Spectre.Console.Style.Plain;
                return Spectre.Console.Style.Parse(v);
            }

            public override void Write(Utf8JsonWriter writer, Style value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value.ToMarkup(), options);
        }
    }
}
