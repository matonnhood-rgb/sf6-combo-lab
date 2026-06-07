using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ComboLab.Models;

public sealed class ObservableDictionary<TKey, TValue>
    : IDictionary<TKey, TValue>, INotifyCollectionChanged, INotifyPropertyChanged
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _items;

    public ObservableDictionary()
        : this(null)
    {
    }

    public ObservableDictionary(IEqualityComparer<TKey>? comparer)
    {
        _items = new Dictionary<TKey, TValue>(comparer);
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TValue this[TKey key]
    {
        get => _items[key];
        set
        {
            _items[key] = value;
            NotifyReset();
        }
    }

    public ICollection<TKey> Keys => _items.Keys;

    public ICollection<TValue> Values => _items.Values;

    public int Count => _items.Count;

    public bool IsReadOnly => false;

    public void Add(TKey key, TValue value)
    {
        _items.Add(key, value);
        NotifyReset();
    }

    public bool ContainsKey(TKey key) => _items.ContainsKey(key);

    public bool Remove(TKey key)
    {
        if (!_items.Remove(key))
        {
            return false;
        }

        NotifyReset();
        return true;
    }

    public bool TryGetValue(TKey key, out TValue value) =>
        _items.TryGetValue(key, out value!);

    public void Add(KeyValuePair<TKey, TValue> item) =>
        Add(item.Key, item.Value);

    public void Clear()
    {
        _items.Clear();
        NotifyReset();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) =>
        ((ICollection<KeyValuePair<TKey, TValue>>)_items).Contains(item);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
        ((ICollection<KeyValuePair<TKey, TValue>>)_items)
            .CopyTo(array, arrayIndex);

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        if (!Contains(item))
        {
            return false;
        }

        return Remove(item.Key);
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
        _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void NotifyReset()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Keys)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Values)));
        CollectionChanged?.Invoke(
            this,
            new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
    }
}
