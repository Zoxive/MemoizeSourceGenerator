using System;

namespace Zoxive.MemoizeSourceGenerator.Attribute
{
    public interface IPartitionKey : IEquatable<IPartitionKey?>
    {
        string DisplayName { get; }
    }

    public readonly struct CompositeKey : IPartitionKey, IEquatable<CompositeKey?>
    {
        public IPartitionKey ParentKey { get; }
        public IPartitionKey ChildKey { get; }

        public CompositeKey(IPartitionKey parentKey, IPartitionKey childKey)
        {
            ParentKey = parentKey;
            ChildKey = childKey;
        }

        public string DisplayName => $"{ParentKey.DisplayName}>{ChildKey.DisplayName}";

        public bool Equals(IPartitionKey? obj)
        {
            return obj is CompositeKey other && Equals(other);
        }

        public bool Equals(CompositeKey? other)
        {
            return ParentKey.Equals(other?.ParentKey) && ChildKey.Equals(other?.ChildKey);
        }

        public override bool Equals(object? obj)
        {
            return obj is CompositeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ParentKey.GetHashCode() * 397) ^ ChildKey.GetHashCode();
            }
        }
    }
}