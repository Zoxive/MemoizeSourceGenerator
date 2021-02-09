using System;

namespace MemoizeSourceGenerator.Attribute
{
    public sealed class GlobalKey : IPartitionKey
    {
        public static readonly GlobalKey Instance = new GlobalKey();

        public string DisplayName { get; }

        private GlobalKey()
        {
            DisplayName = "GLOBAL";
        }

        private bool Equals(GlobalKey other)
        {
            return DisplayName == other.DisplayName;
        }

        public bool Equals(IPartitionKey? obj)
        {
            return ReferenceEquals(this, obj) || obj is GlobalKey other && Equals(other);
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || obj is GlobalKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(DisplayName);
        }
    }
}