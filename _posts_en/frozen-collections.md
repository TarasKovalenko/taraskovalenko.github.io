---
title: "Frozen Collections in .NET 8: fast reads at the cost of slow creation"
author: Taras Kovalenko
date: 2025-02-16 09:00:00.000000000 +02:00
categories:
- ".net"
- performance
- C#
- performance optimization
tags:
- ".net"
- performance
- collections
- C#
- immutable
- frozenCollections
lang: en
locale: en_US
translation_key: frozen-collections
permalink: "/en/posts/frozen-collections/"
---

.NET 8 added a new kind of collection: Frozen Collections. They're meant for data that's created once and then read a lot, and reading is what they're optimized for.
Once created, Frozen Collections are completely immutable. That opens up optimizations mutable collections can't do: for example, the data structure can be tuned to the specific data set, its characteristics and how the values are distributed.

---

## Types of Frozen Collections

The following types of Frozen Collections are available in .NET 8:

* `FrozenDictionary<TKey, TValue>` - immutable read-only dictionary optimized for fast lookup and enumeration
* `FrozenSet<T>` - immutable read-only set optimized for fast search and enumeration

They're useful wherever data rarely changes.
For example, you can keep your application's configuration in a FrozenDictionary: it's loaded when the server starts and never changes afterwards. Reading settings stays fast, and you don't need to synchronize threads.

---

## Why Frozen Collections are faster than normal collections

## Optimized internal structure

The data in a Frozen Collection won't change, so the collection can shape its structure around that specific set. `FrozenDictionary`, for example, picks how to store and look up data based on the key type and how the keys are distributed. A regular collection can't do that.

## Lack of synchronization

An immutable collection doesn't need synchronization to be thread-safe. In multi-threaded code that's a noticeable win.

## Specialized implementations

Integers and strings get their own implementations, with algorithms built around how those types behave.

## Compact placement in memory

The collection's size never changes, so the data can be laid out more compactly in memory. That means better locality and fewer CPU cache misses. And since memory never has to be reallocated, it doesn't get fragmented either.

## Optimizations during creation

Some of the work happens up front, at creation time, tailored to the specific data set. Small data sets may get a plain array instead of a hash table, and direct access via `GetValueRefOrNullRef` avoids unnecessary copying.

You feel all of this most when data is read heavily and from several threads. On top of that, the collection never has to check for state changes or track versions, which also helps speed.

---

## Interesting features from the code

Here are a few interesting spots in the [FrozenDictionary](https://github.com/dotnet/runtime/blob/5535e31a712343a63f5d7d796cd874e563e5ac14/src/libraries/System.Collections.Immutable/src/System/Collections/Frozen/FrozenDictionary.cs){:target="_blank"} implementation:

### Optimization for different types of keys

```cs
if (typeof(TKey).IsValueType && ReferenceEquals(comparer, EqualityComparer<TKey>.Default))
{
    if (source.Count <= Constants.MaxItemsInSmallValueTypeFrozenCollection)
    {
        if (Constants.IsKnownComparable<TKey>())
        {
            return new SmallValueTypeComparableFrozenDictionary<TKey, TValue>(source);
        }
        return new SmallValueTypeDefaultComparerFrozenDictionary<TKey, TValue>(source);
    }
}
```

FrozenDictionary has dedicated optimizations for value types. If the collection is small and uses the default comparer, a specialized implementation is chosen.

### Advanced optimizations for strings

```cs
if (typeof(TKey) == typeof(string) &&
    (ReferenceEquals(comparer, EqualityComparer<TKey>.Default) || 
     ReferenceEquals(comparer, StringComparer.Ordinal) || 
     ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase)))
{
    // Analysis of keys for optimal storage
    KeyAnalyzer.AnalysisResults analysis = KeyAnalyzer.Analyze(
        keys, 
        ReferenceEquals(stringComparer, StringComparer.OrdinalIgnoreCase), 
        minLength, 
        maxLength
    );
}
```

For string keys, the collection analyzes the keys themselves and picks a storage and lookup strategy based on the result.

### Efficient value retrieval

```cs
public ref readonly TValue GetValueRefOrNullRef(TKey key)
{
    if (key is null)
    {
        ThrowHelper.ThrowArgumentNullException(nameof(key));
    }
    return ref GetValueRefOrNullRefCore(key);
}
```

The method returns a reference to the value, which allows you to avoid copying large objects.

### Optimized enumerator

```cs
public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
{
    private readonly TKey[] _keys;
    private readonly TValue[] _values;
    private int _index;

    internal Enumerator(TKey[] keys, TValue[] values)
    {
        Debug.Assert(keys.Length == values.Length);
        _keys = keys;
        _values = values;
        _index = -1;
    }
}
```

The enumerator is a `struct`, so it doesn't cause an allocation on the heap, and it reads the arrays directly.

### Special processing of small collections

```cs
if (source.Count <= Constants.MaxItemsInSmallFrozenCollection)
{
    return new SmallFrozenDictionary<TKey, TValue>(source);
}
```

For small collections, a special implementation is used, which can be more efficient than a hash table.

### Immutability across interfaces

```cs
void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => 
    throw new NotSupportedException();

void ICollection<KeyValuePair<TKey, TValue>>.Clear() => 
    throw new NotSupportedException();

bool IDictionary<TKey, TValue>.Remove(TKey key) => 
    throw new NotSupportedException();
```

All modification methods are explicitly implemented through interfaces and throw an exception, so there's no way to change the collection.

Clearly a lot of work went into `FrozenDictionary` to make reads fast.

---

## Benchmark results

![Benchmark](/assets/img/posts/2025-02-16/benchmark.png)

The benchmark code:

```cs
[MemoryDiagnoser]
public class CollectionsBenchmark
{
    private const int N = 1_000_000;
    private readonly int[] _items;
    private Dictionary<int, string> _dictionary;
    private FrozenDictionary<int, string> _frozenDictionary;
    private HashSet<int> _hashSet;
    private FrozenSet<int> _frozenSet;

    private readonly int[] _lookupItems;

    public CollectionsBenchmark()
    {
        _items = Enumerable.Range(0, N).ToArray();
        _lookupItems = new int[1000];
        var random = new Random(42);
        for (int i = 0; i < _lookupItems.Length; i++)
        {
            _lookupItems[i] = random.Next(N * 2);
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        _dictionary = _items.ToDictionary(x => x, x => x.ToString());
        _frozenDictionary = _items.ToFrozenDictionary(x => x, x => x.ToString());
        _hashSet = new HashSet<int>(_items);
        _frozenSet = _items.ToFrozenSet();
    }

    [Benchmark]
    public void Dictionary_Lookup()
    {
        foreach (var item in _lookupItems)
        {
            _ = _dictionary.TryGetValue(item, out _);
        }
    }

    [Benchmark]
    public void FrozenDictionary_Lookup()
    {
        foreach (var item in _lookupItems)
        {
            _ = _frozenDictionary.TryGetValue(item, out _);
        }
    }

    [Benchmark]
    public void HashSet_Lookup()
    {
        foreach (var item in _lookupItems)
        {
            _ = _hashSet.Contains(item);
        }
    }

    [Benchmark]
    public void FrozenSet_Lookup()
    {
        foreach (var item in _lookupItems)
        {
            _ = _frozenSet.Contains(item);
        }
    }

    [Benchmark]
    public Dictionary<int, string> Dictionary_Creation()
    {
        return _items.ToDictionary(x => x, x => x.ToString());
    }

    [Benchmark]
    public FrozenDictionary<int, string> FrozenDictionary_Creation()
    {
        return _items.ToFrozenDictionary(x => x, x => x.ToString());
    }

    [Benchmark]
    public HashSet<int> HashSet_Creation()
    {
        return new HashSet<int>(_items);
    }

    [Benchmark]
    public FrozenSet<int> FrozenSet_Creation()
    {
        return _items.ToFrozenSet();
    }
}
```

### Search operations (Lookup)

* Dictionary vs FrozenDictionary  
  * Dictionary: 4.242 μs
  * FrozenDictionary: 2.375 µs

Improvement: ~44% faster

* HashSet vs FrozenSet
  * HashSet: 4.263 µs
  * FrozenSet: 2.277 µs

Improvement: ~47% faster

### Creation operations

* Dictionary vs FrozenDictionary
  * Dictionary: 57,155 μs / 71.7 MB
  * FrozenDictionary: 78,529 µs / 115.8 MB

FrozenDictionary is created ~37% slower and uses ~61% more memory

* HashSet vs FrozenSet
  * HashSet: 6,894 µs / 18.6 MB
  * FrozenSet: 14,775 µs / 38.7 MB

FrozenSet is created ~114% slower and uses ~108% more memory

---

## Conclusion

Frozen Collections make sense where data rarely changes but gets read constantly. You pay for the faster lookups at creation time: in the benchmarks above, creation is 37-114% slower and needs 61-108% more memory.
So they don't replace regular collections. Use them when a collection is built once, say at startup, and then read a lot, and when you need a guarantee that nobody will change the data.