---
title: Frozen Collections в .NET 8 - Нова ера незмінних колекцій
author: Taras Kovalenko
date: 2025-02-16 09:00:00 +0200
categories: [.net, performance, C#, performance optimization]
tags: [.net, performance, collections, C#, immutable, frozenCollections]
---

.NET 8 представив новий тип колекцій - Frozen Collections, які призначені для сценаріїв, де дані створюються один раз і потім активно використовуються для читання.
На відміну від звичайних колекцій, вони оптимізовані для максимальної продуктивності при читанні даних.
Основна особливість Frozen Collections полягає в тому, що після створення вони стають повністю незмінними.
Це дозволяє реалізувати низку оптимізацій, які неможливі для змінних колекцій. Наприклад, структура даних може бути оптимізована саме під конкретний набір даних, враховуючи їх особливості та патерни розподілу.

---

## Типи Frozen Collections

В .NET 8 доступні наступні типи Frozen Collections:

* `FrozenDictionary<TKey, TValue>` - незмінний словник лише для читання, оптимізований для швидкого пошуку та перерахування
* `FrozenSet<T>` - незмінний набір лише для читання, оптимізований для швидкого пошуку та перерахування

Frozen Collections особливо корисні при розробці застосунків у випадках, коли ви працюєте з даними, що рідко змінюються.
Наприклад, ви можете використовувати FrozenDictionary для зберігання конфігурацій вашого застосунку, які завантажуються при старті сервера і залишаються незмінними протягом його роботи. Це забезпечить швидкий доступ до налаштувань без потреби в синхронізації між потоками.

---

## Чому Frozen Collections швидші за звичайні колекції

## Оптимізована внутрішня структура

Frozen Collections оптимізують свою структуру під конкретний набір даних, оскільки знають, що дані не будуть змінюватися. Наприклад, `FrozenDictionary` обирає найефективніший спосіб зберігання та пошуку даних, базуючись на типі ключів та їх розподілі, що неможливо для звичайних колекцій.

## Відсутність синхронізації

На відміну від звичайних колекцій, Frozen Collections не потребують механізмів синхронізації для забезпечення потокобезпечності, оскільки вони незмінні за своєю природою. Це значно підвищує продуктивність у багатопотокових сценаріях.

## Спеціалізовані реалізації

Для різних типів даних використовуються оптимізовані реалізації. Наприклад, для цілих чисел та рядків застосовуються спеціальні алгоритми, що враховують особливості цих типів для покращення продуктивності.

## Компактне розміщення в пам'яті

Незмінний розмір дозволяє розміщувати дані більш компактно в пам'яті, що покращує локальність даних та зменшує кількість промахів кешу процесора. Також відсутня необхідність у перевиділенні пам'яті, що запобігає її фрагментації.

## Оптимізації при створенні

При створенні виконуються попередні обчислення та оптимізації, специфічні для конкретного набору даних. Наприклад, для малих наборів даних може використовуватися простий масив замість хеш-таблиці, а прямий доступ до даних через `GetValueRefOrNullRef` дозволяє уникнути зайвого копіювання.

Всі ці оптимізації разом забезпечують значний приріст у продуктивності, особливо в сценаріях з інтенсивним читанням даних та паралельним доступом.
При цьому відсутність необхідності в перевірках на зміни стану та версіонуванні додатково покращує швидкодію.

---

## Цікаві особливості з коду

Розглянемо декілька цікавих особливостей реалізації [FrozenDictionary](https://github.com/dotnet/runtime/blob/5535e31a712343a63f5d7d796cd874e563e5ac14/src/libraries/System.Collections.Immutable/src/System/Collections/Frozen/FrozenDictionary.cs){:target="_blank"}:

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

Цей фрагмент показує, що FrozenDictionary має спеціальні оптимізації для value types. Якщо колекція невелика і використовує стандартний компаратор, вибирається спеціалізована реалізація для покращення продуктивності.

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

Для string-ключів реалізовано складний аналіз для вибору оптимальної стратегії зберігання та пошуку.

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

Енумератор реалізований як `struct` для уникнення allocation на heap та використовує прямий доступ до масивів.

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

Всі методи модифікації явно реалізовані через інтерфейси і викидають виключення, що гарантує незмінність колекції.

Ці оптимізації демонструють, наскільки глибоко продумана реалізація `FrozenDictionary` для забезпечення максимальної продуктивності в різних сценаріях використання.

---

## Результати бенчмарків

![Benchmark](/assets/img/posts/2025-02-16/benchmark.png)

Код на якому збиралися бенчмарки:

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

Frozen Collections - це потужний інструмент для оптимізації продуктивності в сценаріях, де дані рідко змінюються, але часто читаються.
Завдяки спеціалізованим реалізаціям для різних типів даних та розмірів колекцій, вони забезпечують максимальну ефективність при мінімальному використанні пам'яті.
При цьому важливо розуміти, що ці колекції не є заміною звичайним колекціям у всіх сценаріях - їх варто використовувати саме там, де потрібна максимальна продуктивність читання та гарантована незмінність даних.
