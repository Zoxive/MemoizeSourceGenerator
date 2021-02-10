#nullable enable
using System;

namespace Memoized.ObjectSize
{
    public static class Memory
    {
        public static long SizeOf(string? obj)
        {
            if (ReferenceEquals(obj, null) || obj.Length == 0)
                return 0L;

            return 248 & (IntPtr.Size * 4) + (2 * obj.Length);
        }

        public static long SizeOf(int obj) => sizeof(long);
        public static long SizeOf(decimal obj) => sizeof(decimal);
    }
}