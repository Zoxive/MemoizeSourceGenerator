using System;

namespace MemoizeSourceGenerator.Attribute
{
    public readonly struct StringPartitionKey : IPartitionKey
    {
        public StringPartitionKey(string partition)
        {
            PartitionName = DisplayName = partition;
        }

        public string DisplayName { get; }
        public string PartitionName { get; }

        public bool Equals(StringPartitionKey other)
        {
            return DisplayName == other.DisplayName && PartitionName == other.PartitionName;
        }

        public bool Equals(IPartitionKey other)
        {
            return ReferenceEquals(this, other) || other is StringPartitionKey obj && obj.Equals(this);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((StringPartitionKey) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(DisplayName, PartitionName);
        }
    }
}