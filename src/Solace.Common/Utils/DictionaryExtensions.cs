namespace Solace.Common.Utils;

public static class DictionaryExtensions
{
    extension<TKey, TValue>(IDictionary<TKey, TValue> dicionary)
    {
        public void AddRange(IReadOnlyDictionary<TKey, TValue> value)
        {
            foreach (var item in value)
            {
                dicionary[item.Key] = item.Value;
            }
        }

        public TValue? ComputeIfAbsent(TKey key, Func<TKey, TValue?> mappingFunction)
        {
            if (dicionary.TryGetValue(key, out TValue? value))
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
                    dicionary.Add(key, newValue);
                    return newValue;
                }
            }
        }

        public void RemoveAll(Predicate<KeyValuePair<TKey, TValue>> predicate)
        {
            List<TKey> toRemove = [];

            foreach (var item in dicionary)
            {
                if (predicate(item))
                {
                    toRemove.Add(item.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                dicionary.Remove(toRemove[i]);
            }
        }
    }
}
