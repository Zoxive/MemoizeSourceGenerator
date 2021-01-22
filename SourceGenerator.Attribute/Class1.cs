using System;

namespace SourceGenerator.Attribute
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    public class CreateMemoizedImplementationAttribute : System.Attribute
    {
        public string? Name { get; set; }
    }
}