public static class Ext
{
    public static Func<TIn, TResult> Then<TIn, TOut, TResult>(this Func<TIn, TOut> func, Func<TIn, TOut, TResult> then)
    => i => then(i, func(i));

    public static IEnumerable<T> ReportProgress<T>(this IEnumerable<T> items, long max, int steps)
    {
        if (steps <= 0)
            return items;
        return Report(items, max, steps, steps is 100 ? Report100 : Report1000);

        static IEnumerable<T> Report(IEnumerable<T> items, long max, int steps, Action<double, long, long> report)
        {
            var percent = Math.Max(max / steps, 1);
            using var enumerator = items.GetEnumerator();

            for (var i = 0L; enumerator.MoveNext(); i++)
            {
                if (i % percent == 0)
                    report(i * 100d / max, i, max);
                yield return enumerator.Current;
            }
            report(100, max, max);
        }

        static void Report100(double percent, long i, long max)
        => Console.Error.WriteLine($"[{new string('=', Math.Min(48, (int)percent)),-48}{percent,3:##0}%{new string('=', Math.Max(0, ((int)percent) - 52)),-48}] ({i}/{max})");

        static void Report1000(double percent, long i, long max)
        => Console.Error.WriteLine($"[{new string('=', Math.Min(47, (int)percent)),-47}{percent,5:##0.0}%{new string('=', Math.Max(0, ((int)percent) - 53)),-47}] ({i}/{max})");
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
}
