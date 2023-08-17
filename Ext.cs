using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

public class CancelException : Exception
{ }

[DebuggerStepThrough]
public static class Ext
{
    public static Func<TIn, TResult> Then<TIn, TOut, TResult>(this Func<TIn, TOut> func, Func<TIn, TOut, TResult> then)
    => i => then(i, func(i));

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

    internal static void Save(IReadOnlyList<Letter> guesses, int length, IEnumerable<char[]> candidates, IReadOnlyDictionary<char, (int? qty, int min)> symbolsQty)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            },
        };
        var list = candidates
            .Select(static c => new string(c))
            .ToList();
        var g = guesses
            .Select(static g => g
                .SelectAll(static l => l.Next!, static l =>l is not null)
                .Select(static l => new Saving.Guess(l.Selected, l.LetterMode))
                .ToArray())
            .ToList();
        var saving = new Saving(length, symbolsQty.ToDictionary(static kvp => kvp.Key, static kvp => new Saving.Qty(kvp.Value.qty, kvp.Value.min)), g, list);
        var jsonString = JsonSerializer.Serialize(saving, options);
        File.WriteAllText("output.json", jsonString);
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

    private sealed record class Saving(int Length, Dictionary<char, Saving.Qty> Symbols, List<Saving.Guess[]> Guesses, List<string> Candidates)
    {
        [JsonPropertyOrder(-1)]
        public int Version
        {
            get => 1;
            init
            {
                if (value != Version)
                    throw new InvalidOperationException();
            }
        }

        public sealed record class Qty(int? Quantity, int Minimum);
        public sealed record class Guess(char Value, LetterMode Mode);
    }
}
