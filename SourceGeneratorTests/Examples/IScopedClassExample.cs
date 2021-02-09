using MemoizeSourceGenerator.Attribute;

namespace SourceGeneratorTests.Examples
{
    [CreateMemoizedImplementation(Name = "Memoized_ScopedClassExample", MemoizerFactory = typeof(TenantSpecificMemoizerFactory))]
    public interface IScopedClassExample
    {
        IValueType1 Add(int arg1, int arg2);

        ValueType1 TestPartition([PartitionCache]string partitionName, int arg2);
    }

    public sealed class ScopedClassExample : IScopedClassExample
    {
        public IValueType1 Add(int arg1, int arg2)
        {
            return new ValueType1(arg1 + arg2);
        }

        public ValueType1 TestPartition(string partitionName, int arg2)
        {
            return new ValueType1(partitionName.Length * arg2);
        }
    }
}