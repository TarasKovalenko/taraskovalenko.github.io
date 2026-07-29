---
title: Frozen Collections in .NET 8 - A new era of immutable collections
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

.NET 8 introduced a new type of collections - Frozen Collections, which are designed for scenarios where data is created once and then actively read.
Unlike regular collections, they are optimized for maximum performance when reading data.
The main feature of Frozen Collections is that they become completely immutable once created.
This allows for a number of optimizations that are not possible with mutable collections. For example, the data structure can be optimized specifically for a specific data set, taking into account their features and distribution patterns.

---

## Types of Frozen Collections

The following types of Frozen Collections are available in .NET 8:

* `FrozenDictionary<TKey, TValue>` - immutable read-only dictionary optimized for fast lookup and enumeration
* `FrozenSet<T>` - immutable read-only set optimized for fast search and enumeration

Frozen Collections are especially useful when developing applications where you work with data that rarely changes.
For example, you can use FrozenDictionary to store the configurations of your application, which are loaded when the server starts and remain unchanged during its operation. This will provide quick access to settings without the need for synchronization between threads.

---

## Why Frozen Collections are faster than normal collections

## Optimized internal structure

Frozen Collections optimize their structure for a specific set of data because they know that the data will not change. For example, `FrozenDictionary` chooses the most efficient way to store and retrieve data based on the type of keys and their distribution, which is impossible for regular collections.

## Lack of synchronization

Unlike regular collections, Frozen Collections do not require synchronization mechanisms to ensure thread safety because they are immutable in nature. This greatly improves performance in multi-threaded scenarios.

## Specialized implementations

Optimized implementations are used for different data types. For example, for integers and strings, special algorithms are applied that take into account the peculiarities of these types to improve performance.

## Compact placement in memory

Fixed size allows data to be placed more compactly in memory, which improves data locality and reduces the number of processor cache misses. There is also no need to reallocate memory, which prevents its fragmentation.

## Optimizations during creation

During creation, pre-calculations and optimizations specific to a particular data set are performed. For example, a simple array can be used instead of a hash table for small data sets, and direct data access via `GetValueRefOrNullRef` avoids unnecessary copying.

All these optimizations together provide a significant increase in performance, especially in scenarios with intensive data reading and parallel access.
At the same time, the lack of need for state change checks and versioning additionally improves performance.

---

## Interesting features from the code

Let's consider some interesting features of the [FrozenDictionary](https://github.com/dotnet/runtime/blob/5535e31a712343a63f5d7d796cd874e563e5ac14/src/libraries/System.Collections.Immutable/src/System/Collections/Frozen/FrozenDictionary.cs){:target="_blank"} implementation:

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

This snippet shows that FrozenDictionary has special optimizations for value types. If the collection is small and uses a standard comparator, a specialized implementation is chosen to improve performance.

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

For string keys, a complex analysis is implemented to select the optimal storage and search strategy.

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

The enumerator is implemented as `struct` to avoid allocation on the heap and uses direct access to arrays.

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

All modification methods are explicitly implemented through interfaces and throw an exception, which guarantees the immutability of the collection.

These optimizations demonstrate how deeply the `FrozenDictionary` implementation has been thought through to ensure maximum performance in various usage scenarios.

---

## Benchmark results

![Benchmark](/assets/img/posts/2025-02-16/benchmark.png)

The code on which the benchmarks were compiled:

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

Frozen Collections are a powerful tool for optimizing performance in scenarios where data rarely changes but is frequently read.
With specialized implementations for different data types and collection sizes, they provide maximum efficiency with minimal memory usage.
At the same time, it is important to understand that these collections are not a replacement for ordinary collections in all scenarios - they should be used precisely where maximum reading performance and guaranteed data immutability are required.