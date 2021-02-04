using System;

namespace MemoizeSourceGenerator.Attribute
{
    public interface IPartitionKey : IEquatable<IPartitionKey?>
    {
        string DisplayName { get; }
    }
}