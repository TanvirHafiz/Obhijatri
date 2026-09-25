namespace Obhijatri.Core;

public sealed record ClosedTab(string Url, string Title, int Index);

/// <summary>Recently closed tabs for Ctrl+Shift+T. In memory only, newest on top.</summary>
public sealed class ClosedTabStack
{
    private readonly int _capacity;
    private readonly LinkedList<ClosedTab> _items = new();

    public ClosedTabStack(int capacity = 25)
    {
        _capacity = capacity;
    }

    public int Count => _items.Count;

    public void Push(ClosedTab tab)
    {
        _items.AddFirst(tab);
        if (_items.Count > _capacity)
        {
            _items.RemoveLast();
        }
    }

    public bool TryPop(out ClosedTab? tab)
    {
        tab = _items.First?.Value;
        if (tab is null)
        {
            return false;
        }
        _items.RemoveFirst();
        return true;
    }
}
