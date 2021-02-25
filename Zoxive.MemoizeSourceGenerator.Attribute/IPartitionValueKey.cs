using System;

namespace Zoxive.MemoizeSourceGenerator.Attribute
{
    public interface IPartitionObjectKey : IEquatable<IPartitionObjectKey?>
    {
        public IPartitionKey PartitionKey { get; }
    }

    public readonly struct PartitionObjectKeyString : IPartitionObjectKey, IEquatable<PartitionObjectKeyString>
    {
        public PartitionObjectKeyString(IPartitionKey partitionKey, string key)
        {
            PartitionKey = partitionKey;
            Key = key;
        }

        public IPartitionKey PartitionKey { get; }

        public string Key { get; }

        public bool Equals(PartitionObjectKeyString other)
        {
            return PartitionKey.Equals(other.PartitionKey) && Key == other.Key;
        }

        public bool Equals(IPartitionObjectKey? obj)
        {
            return obj is PartitionObjectKeyString other && Equals(other);
        }

        public override bool Equals(object? obj)
        {
            return obj is PartitionObjectKeyString other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (PartitionKey.GetHashCode() * 397) ^ Key.GetHashCode();
            }
        }
    }
}