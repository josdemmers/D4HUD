using System.Collections;

namespace D4HUD.Extensions
{
    public static class EnumerableExtensions
    {
        //https://stackoverflow.com/questions/15452165/multiple-oftype-linq
        public static IEnumerable OfType<T1, T2>(this IEnumerable source)
        {
            foreach (object item in source)
            {
                if (item is T1 || item is T2)
                {
                    yield return item;
                }
            }
        }
    }
}