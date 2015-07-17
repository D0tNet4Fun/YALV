using System;
using System.Collections.Generic;

namespace YALV.Common
{
    public static class Utils
    {
        public static void AddSorted<TItem, TKey>(this IList<TItem> list, TItem item, Func<TItem, TKey> selector = null, IComparer<TKey> comparer = null)
            where TKey : class
        {
            if (comparer == null)
                comparer = Comparer<TKey>.Default;

            if (selector == null)
            {
                selector = x => x as TKey;
            }

            int i = 0;
            while (i < list.Count && comparer.Compare(selector(list[i]), selector(item)) < 0)
                i++;

            list.Insert(i, item);
        }
    }
}