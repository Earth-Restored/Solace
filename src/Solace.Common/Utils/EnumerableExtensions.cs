namespace Solace.Common.Utils;

public static class EnumerableExtensions
{
    extension<T>(IEnumerable<T?> enumerable)
        where T : struct
    {
        public IEnumerable<T> WhereNotNull()
        {
            foreach (var item in enumerable)
            {
                if (item is not null)
                {
                    yield return item.Value;
                }
            }
        }
    }
}
