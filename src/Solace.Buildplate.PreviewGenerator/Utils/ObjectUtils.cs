using System.Collections;

namespace Solace.Buildplate.PreviewGenerator.Utils;

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

        if (obj1 is byte[] b1 && obj2 is byte[] b2)
        {
            return b1.AsSpan().SequenceEqual(b2);
        }

        if (obj1 is int[] i1 && obj2 is int[] i2)
        {
            return i1.AsSpan().SequenceEqual(i2);
        }

        if (obj1 is long[] l1 && obj2 is long[] l2)
        {
            return l1.AsSpan().SequenceEqual(l2);
        }

        if (obj1.Equals(obj2))
        {
            return true;
        }

        if (obj1 is IEnumerable enumerable1 && obj2 is IEnumerable enumerable2)
        {
            var enumerator1 = enumerable1.GetEnumerator();
            var enumerator2 = enumerable2.GetEnumerator();

            try
            {
                while (enumerator1.MoveNext())
                {
                    if (!enumerator2.MoveNext())
                    {
                        return false;
                    }

                    if (!DeepEquals(enumerator1.Current, enumerator2.Current))
                    {
                        return false;
                    }
                }

                return !enumerator2.MoveNext();
            }
            finally
            {
                (enumerator1 as IDisposable)?.Dispose();
                (enumerator2 as IDisposable)?.Dispose();
            }
        }

        return false;
    }

    public static int GetDeepHashCode(object? obj)
    {
        if (obj is null)
        {
            return 0;
        }

        if (obj is byte[] bytes)
        {
            var hash = new HashCode();
            hash.AddBytes(bytes);
            return hash.ToHashCode();
        }

        if (obj is int[] ints)
        {
            var hash = new HashCode();
            foreach (var item in ints)
            {
                hash.Add(item);
            }

            return hash.ToHashCode();
        }

        if (obj is long[] longs)
        {
            var hash = new HashCode();
            foreach (var item in longs)
            {
                hash.Add(item);
            }

            return hash.ToHashCode();
        }

        if (obj is IEnumerable enumerable and not string)
        {
            var hash = new HashCode();
            foreach (var item in enumerable)
            {
                hash.Add(GetDeepHashCode(item));
            }

            return hash.ToHashCode();
        }

        return obj.GetHashCode();
    }
}