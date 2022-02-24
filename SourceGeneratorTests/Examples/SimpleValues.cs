using System;
using Zoxive.MemoizeSourceGenerator.Attribute;

namespace SourceGeneratorTests.Examples
{
    [CreateMemoizedImplementation(Name = "Memoized_SimpleValues")]
    public interface ISimpleValues
    {
        int Add(int arg1, int arg2);

        [SlidingCache(2)]
        string Result(int arg1);

        decimal GetPrice([PartitionCache] string name);

        [return: SizeOfResult(GlobalStaticMethod = "SourceGeneratorTests.Examples.SimpleValuesExtensions.Explodes")]
        decimal ExplodesOnSizeOf(decimal arg);
    }

    public sealed class SimpleValues : ISimpleValues
    {
        public int Add(int arg1, int arg2) => arg1 + arg2;
        public string Result(int arg1) => $"{arg1}";
        public decimal GetPrice(string name) => name.Length * 2;
        public decimal ExplodesOnSizeOf(decimal arg) => arg * 2;
    }

    public static class SimpleValuesExtensions
    {
        public static long Explodes(decimal arg)
        {
            throw new ArgumentOutOfRangeException(nameof(arg), "This sizeof method explodes..what happens?");
        }
    }
}