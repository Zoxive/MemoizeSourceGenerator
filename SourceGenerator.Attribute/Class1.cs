using System;

namespace SourceGenerator.Attribute
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    public class CreateMemoizedImplementationAttribute : System.Attribute
    {
        public string? Name { get; set; }
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

    // tODO Configurable options
    public static class MemoizedInterfaceOptions
    {
        public static readonly TimeSpan DefaultExpirationTime = TimeSpan.FromMinutes(10);
    }
}