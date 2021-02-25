using System;

namespace Zoxive.MemoizeSourceGenerator.Attribute
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    public class CreateMemoizedImplementationAttribute : System.Attribute
    {
        public string? Name { get; set; }

        public Type? MemoizerFactory { get; set; }
    }

    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, Inherited = false)]
    public class SlidingCacheAttribute : System.Attribute
    {
        public double InMinutes { get; }

        public SlidingCacheAttribute(double inMinutes)
        {
            InMinutes = inMinutes;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public class PartitionCacheAttribute : System.Attribute
    {
    }

    [AttributeUsage(AttributeTargets.ReturnValue | AttributeTargets.Assembly)]
    public class SizeOfResultAttribute : System.Attribute
    {
        public string? GlobalStaticMethod { get; set; }

        public string? SizeOfMethodName { get; set; }
    }
}