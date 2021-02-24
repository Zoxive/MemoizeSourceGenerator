namespace MemoizeSourceGenerator.Attribute
{
    public readonly struct StringPartitionKey : IPartitionKey
    {
        public StringPartitionKey(string partition)
        {
            DisplayName = partition;
        }

        public string DisplayName { get; }

        public bool Equals(StringPartitionKey other)
        {
            return DisplayName == other.DisplayName;
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
            return DisplayName.GetHashCode();
        }
    }
}