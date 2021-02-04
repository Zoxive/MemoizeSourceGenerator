using System;

namespace MemoizeSourceGenerator.Attribute
{
    public readonly struct StringPartitionKey : IPartitionKey
    {
        private readonly string _partition;

        public StringPartitionKey(string partition)
        {
            _partition = partition;
        }

        public string DisplayName => _partition;

        public bool Equals(StringPartitionKey other)
        {
            return _partition == other._partition;
        }

        public bool Equals(IPartitionKey? obj)
        {
            return obj is StringPartitionKey other && other.Equals(this);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is StringPartitionKey other && other.Equals(this);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_partition);
        }
    }
}