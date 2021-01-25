using System;

namespace SourceGenerator.Attribute
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    public class CreateMemoizedImplementationAttribute : System.Attribute
    {
        public string? Name { get; set; }
    }

    // tODO Configurable options
    public static class MemoizedInterfaceOptions
    {
        public static double DefaultCacheDurationFactor { get; set; } = 1.0d;
        public static readonly TimeSpan DefaultExpirationTime = TimeSpan.FromMinutes(10);
    }
}