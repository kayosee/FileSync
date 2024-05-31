using Force.Crc32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Tools;

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
        for (var i = 0; i < count; i++)
        {
            source.SetValue(func.Invoke((TSource)source.GetValue(i)), i);
        }
    }
    public static void Xor(this byte[] source, byte key)
    {
        if (source == null)
        {
            throw new ArgumentNullException("source");
        }

        Span<byte> span = source;
        for (int i = 0; i < span.Length; i++)
        {
            span[i] ^= key;
        }
    }
    public static bool IsEqualsWith(this byte[] a, byte[] b)
    {
        Span<byte> span = a;
        Span<byte> otherSpan = b;

        if(span.Length != otherSpan.Length) 
        { 
            return false;
        }
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) 
                return false;
        }
        return true;
    }
    public static uint GetCrc32(this string text)
    {
        var buffer = Encoding.UTF8.GetBytes(text, 0, text.Length);
        return Crc32Algorithm.Compute(buffer);
    }
}
