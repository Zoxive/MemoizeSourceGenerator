#nullable enable
using System;

namespace SourceGeneratorTests
{
    public static class SizeOfObject
    {
        public static long SizeOf(string? obj)
        {
            if (ReferenceEquals(obj, null) || obj.Length == 0)
                return 0L;

            return 248 & (IntPtr.Size * 4) + (2 * obj.Length);
        }

        public static long SizeOf(int obj) => sizeof(long);
        public static long SizeOf(decimal obj) => sizeof(decimal);
        public static long SizeOf<T>(T obj) => 0;
    }
}