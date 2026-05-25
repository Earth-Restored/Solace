namespace Solace.Common.Utils;

public static class DictionaryExtensions
{
    public static void AddRange<TKey, TValue>(this IDictionary<TKey, TValue> dic, IReadOnlyDictionary<TKey, TValue> dicToAdd)
    {
        foreach (var item in dicToAdd)
        {
            dic[item.Key] = item.Value;
        }
    }

    public static TValue? ComputeIfAbsent<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key, Func<TKey, TValue?> mappingFunction)
    {
        if (dic.TryGetValue(key, out TValue? value))
        {
            return value;
        }
        else
        {
            TValue? newValue = mappingFunction(key);
            if (newValue is null)
            {
                return default;
            }
            else
            {
                dic.Add(key, newValue);
                return newValue;
            }
        }
    }

    public static void RemoveAll<TKey, TValue>(this IDictionary<TKey, TValue> dic, Predicate<KeyValuePair<TKey, TValue>> predicate)
    {
        List<TKey> toRemove = [];

        foreach (var item in dic)
        {
            if (predicate(item))
            {
                toRemove.Add(item.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            dic.Remove(toRemove[i]);
        }
    }
}
