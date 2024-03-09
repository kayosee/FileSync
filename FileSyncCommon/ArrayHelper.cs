using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public static class ArrayHelper
{
    public static TSource[] Apply<TSource>(this IEnumerable<TSource> source, Func<TSource, TSource> func)
    {
        if (source == null)
        {
            throw new ArgumentNullException("source");
        }

        var result = (from r in source select func.Invoke(r)).ToArray();
        
        return result;
    }

    public static void ForEach<TSource>(this Array source, Func<TSource, TSource> func)
    {
        if (source == null)
        {
            throw new ArgumentNullException("source");
        }
        var count = source.Length;
        if (count == 0)
        {
            return;
        }
        for( var i = 0;i < count; i++ )
        {
            source.SetValue(func.Invoke((TSource)source.GetValue(i)), i);
        }
    }
}
