---
title: "Frozen Collections у .NET 8: швидке читання ціною повільного створення"
author: Taras Kovalenko
date: 2025-02-16 09:00:00 +0200
categories: [.net, performance, C#, performance optimization]
tags: [.net, performance, collections, C#, immutable, frozenCollections]
---

У .NET 8 з'явився новий тип колекцій - Frozen Collections. Вони для випадків, коли дані створюються один раз, а потім їх багато читають, і саме читання в них оптимізоване.
Після створення Frozen Collections повністю незмінні. Через це в них можливі оптимізації, недоступні змінним колекціям: наприклад, структуру даних можна підлаштувати під конкретний набір даних, його особливості та розподіл значень.

---

## Типи Frozen Collections

В .NET 8 доступні наступні типи Frozen Collections:

* `FrozenDictionary<TKey, TValue>` - незмінний словник лише для читання, оптимізований для швидкого пошуку та перерахування
* `FrozenSet<T>` - незмінний набір лише для читання, оптимізований для швидкого пошуку та перерахування

Вони стають у пригоді там, де дані рідко змінюються.
Наприклад, у FrozenDictionary можна тримати конфігурацію застосунку, яка завантажується при старті сервера і більше не змінюється. Доступ до налаштувань буде швидким, і синхронізувати потоки не доведеться.

---

## Чому Frozen Collections швидші за звичайні колекції

## Оптимізована внутрішня структура

Дані в Frozen Collections не змінюватимуться, тому колекція може підлаштувати свою структуру під конкретний набір. `FrozenDictionary`, наприклад, обирає спосіб зберігання та пошуку залежно від типу ключів і їх розподілу. Звичайна колекція так не може.

## Відсутність синхронізації

Незмінну колекцію не треба синхронізувати, щоб вона була потокобезпечною. У багатопотоковому коді це дає помітний виграш.

## Спеціалізовані реалізації

Для цілих чисел і рядків є окремі реалізації з алгоритмами, які враховують особливості саме цих типів.

## Компактне розміщення в пам'яті

Розмір колекції не змінюється, тому дані можна покласти в пам'ять компактніше. Від цього краща локальність і менше промахів кешу процесора. А оскільки пам'ять не треба перевиділяти, вона й не фрагментується.

## Оптимізації при створенні

Частину роботи колекція робить наперед, під час створення, під конкретний набір даних. Для малих наборів це може бути простий масив замість хеш-таблиці, а прямий доступ через `GetValueRefOrNullRef` прибирає зайве копіювання.

Найбільше все це відчувається, коли даних читають багато і з кількох потоків. До того ж колекції не треба перевіряти зміни стану чи вести версії, і це теж додає швидкості.

---

## Цікаві особливості з коду

Ось кілька цікавих місць у реалізації [FrozenDictionary](https://github.com/dotnet/runtime/blob/5535e31a712343a63f5d7d796cd874e563e5ac14/src/libraries/System.Collections.Immutable/src/System/Collections/Frozen/FrozenDictionary.cs){:target="_blank"}:

### Оптимізація для різних типів ключів

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

У FrozenDictionary є окремі оптимізації для value types. Якщо колекція невелика і використовує стандартний компаратор, обирається спеціалізована реалізація.

### Розширені оптимізації для рядків

```cs
if (typeof(TKey) == typeof(string) &&
    (ReferenceEquals(comparer, EqualityComparer<TKey>.Default) || 
     ReferenceEquals(comparer, StringComparer.Ordinal) || 
     ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase)))
{
    // Аналіз ключів для оптимального зберігання
    KeyAnalyzer.AnalysisResults analysis = KeyAnalyzer.Analyze(
        keys, 
        ReferenceEquals(stringComparer, StringComparer.OrdinalIgnoreCase), 
        minLength, 
        maxLength
    );
}
```

Для string-ключів колекція аналізує самі ключі і за результатом обирає стратегію зберігання та пошуку.

### Ефективне отримання значень

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

Метод повертає reference на значення, що дозволяє уникнути копіювання великих об'єктів.

### Оптимізований енумератор

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

Енумератор реалізований як `struct`, тож не створює allocation на heap, і читає масиви напряму.

### Спеціальна обробка малих колекцій

```cs
if (source.Count <= Constants.MaxItemsInSmallFrozenCollection)
{
    return new SmallFrozenDictionary<TKey, TValue>(source);
}
```

Для невеликих колекцій використовується спеціальна реалізація, яка може бути ефективнішою за хеш-таблицю.

### Незмінність через інтерфейси

```cs
void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => 
    throw new NotSupportedException();

void ICollection<KeyValuePair<TKey, TValue>>.Clear() => 
    throw new NotSupportedException();

bool IDictionary<TKey, TValue>.Remove(TKey key) => 
    throw new NotSupportedException();
```

Всі методи модифікації явно реалізовані через інтерфейси і викидають виключення, тому змінити колекцію неможливо.

Видно, що в `FrozenDictionary` вклали чимало роботи заради швидкого читання.

---

## Результати бенчмарків

![Benchmark](/assets/img/posts/2025-02-16/benchmark.png)

Код бенчмарків:

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

### Операції пошуку (Lookup)

* Dictionary vs FrozenDictionary  
  * Dictionary: 4.242 мкс
  * FrozenDictionary: 2.375 мкс

Покращення: ~44% швидше

* HashSet vs FrozenSet
  * HashSet: 4.263 мкс
  * FrozenSet: 2.277 мкс

Покращення: ~47% швидше

### Операції створення (Creation)

* Dictionary vs FrozenDictionary
  * Dictionary: 57,155 мкс / 71.7 MB
  * FrozenDictionary: 78,529 мкс / 115.8 MB

FrozenDictionary створюється на ~37% повільніше та використовує на ~61% більше пам'яті

* HashSet vs FrozenSet
  * HashSet: 6,894 мкс / 18.6 MB
  * FrozenSet: 14,775 мкс / 38.7 MB

FrozenSet створюється на ~114% повільніше та використовує на ~108% більше пам'яті

---

## Висновок

Frozen Collections мають сенс там, де дані рідко змінюються, а читаються постійно. За швидкий пошук ви платите при створенні: за бенчмарками вище воно на 37-114% повільніше і потребує на 61-108% більше пам'яті.
Тому звичайні колекції вони не замінюють. Беріть їх тоді, коли колекцію створюють один раз, наприклад при старті, а потім багато читають, і коли потрібна гарантія, що дані ніхто не змінить.
