using System.Diagnostics;

[DebuggerStepThrough]
public static class Ext
{
    public static Func<TIn, TResult> Then<TIn, TOut, TResult>(this Func<TIn, TOut> func, Func<TIn, TOut, TResult> then)
    => i => then(i, func(i));

    public static IEnumerable<T> ReportProgress<T>(this IEnumerable<T> items, long max, int steps, TextWriter? writer)
    {
        if (steps <= 0 || writer is null)
            return items;
        return Report(items, max, steps, writer, steps is 100 ? Report100 : Report1000);

        static IEnumerable<T> Report(IEnumerable<T> items, long max, int steps, TextWriter writer, Action<double, long, long, TextWriter> report)
        {
            var percent = Math.Max(max / steps, 1);
            using var enumerator = items.GetEnumerator();

            for (var i = 0L; enumerator.MoveNext(); i++)
            {
                if (i % percent == 0)
                    report(i * 100d / max, i, max, writer);
                yield return enumerator.Current;
            }
            report(100, max, max, writer);
        }

        static void Report100(double percent, long i, long max, TextWriter writer)
        => writer.WriteLine($"[{new string('=', Math.Min(48, (int)percent)),-48}{percent,3:##0}%{new string('=', Math.Max(0, ((int)percent) - 52)),-48}] ({i}/{max})");

        static void Report1000(double percent, long i, long max, TextWriter writer)
        => writer.WriteLine($"[{new string('=', Math.Min(47, (int)percent)),-47}{percent,5:##0.0}%{new string('=', Math.Max(0, ((int)percent) - 53)),-47}] ({i}/{max})");
    }

    internal static Nerdle WithProbalities(this Nerdle nerdle, float[,]? probalities, float minProb = float.Epsilon)
    => probalities is null
        ? new Nerdle()
        {
            Slot = nerdle.Slot,
            Symbols = nerdle.Symbols,
        }
        : new NerdleProbalistic()
        {
            Slot = nerdle.Slot,
            Symbols = nerdle.InitialSymbols,
            Probalities = probalities,
            MinProb = minProb,
        };
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

    public readonly static char[]? Space = { ' ' };
}
