public abstract class Cache<T>
{
    private List<T> _items = new();
    private bool _isDirty = true;

    protected abstract List<T> Rebuild();

    public void MarkDirty()
    {
        _isDirty = true;
    }

    public void EnsureUpToDate()
    {
        if (!_isDirty)
            return;

        _items = Rebuild() ?? new List<T>();
        _isDirty = false;
    }

    public int Count
    {
        get
        {
            EnsureUpToDate();
            return _items.Count;
        }
    }

    public T this[int index]
    {
        get
        {
            EnsureUpToDate();
            return _items[index];
        }
    }

    public IReadOnlyList<T> Items
    {
        get
        {
            EnsureUpToDate();
            return _items;
        }
    }
}