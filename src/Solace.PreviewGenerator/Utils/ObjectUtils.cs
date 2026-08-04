using System.Collections;
using System.Reflection;

namespace Solace.PreviewGenerator.Utils;

public static class ObjectUtils
{
    public static bool DeepEquals(object? obj1, object? obj2)
    {
        if (ReferenceEquals(obj1, obj2))
        {
            return true;
        }

        if (obj1 is null || obj2 is null)
        {
            return false;
        }

        var type1 = obj1.GetType();
        var type2 = obj2.GetType();

        if (type1 != type2)
        {
            return false;
        }

        if (type1.IsPrimitive || obj1 is string)
        {
            return obj1.Equals(obj2);
        }

        if (obj1 is IEnumerable enumerable1 && obj2 is IEnumerable enumerable2)
        {
            var enumerator1 = enumerable1.GetEnumerator();
            var enumerator2 = enumerable2.GetEnumerator();

            while (enumerator1.MoveNext() && enumerator2.MoveNext())
            {
                if (!DeepEquals(enumerator1.Current, enumerator2.Current))
                {
                    return false;
                }
            }

            return !(enumerator1.MoveNext() || enumerator2.MoveNext());
        }

        foreach (var property in type1.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value1 = property.GetValue(obj1);
            var value2 = property.GetValue(obj2);

            if (!DeepEquals(value1, value2))
            {
                return false;
            }
        }

        return true;
    }

    public static int GetDeepHashCode(object? obj)
    {
        if (obj is null)
        {
            return 0;
        }

        var type = obj.GetType();

        if (type.IsPrimitive || obj is string)
        {
            return obj.GetHashCode();
        }

        if (obj is IEnumerable enumerable)
        {
            var hash = new HashCode();
            foreach (var item in enumerable)
            {
                hash.Add(GetDeepHashCode(item));
            }

            return hash.ToHashCode();
        }

        var propertyHash = new HashCode();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            var val = property.GetValue(obj);
            propertyHash.Add(GetDeepHashCode(val));
        }

        return propertyHash.ToHashCode();
    }
}
