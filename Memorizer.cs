using PrimaryParameter.SG;

public sealed partial class Memorizer<T>([Field]IEnumerator<T> enumerator) : IEnumerable<T>, IReadOnlyList<T>
{
    private readonly List<T> _list = new();

    public int Count => ((IReadOnlyCollection<T>)_list).Count;

    public T this[int index] => ((IReadOnlyList<T>)_list)[index];

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    => GetEnumerator();
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    => GetEnumerator();

    public IEnumerator<T> GetEnumerator()
    {
        foreach (var item in _list)
            yield return item;
        while (_enumerator.MoveNext())
        {
            _list.Add(_enumerator.Current);
            yield return _enumerator.Current;
        }
    }
}
