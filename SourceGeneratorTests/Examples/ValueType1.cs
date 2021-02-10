using System;

namespace SourceGeneratorTests.Examples
{
    public class ValueType1 : IValueType1
    {
        public int Value { get; }

        public ValueType1(int value)
        {
            Value = value;
        }

        public bool Equals(IValueType1 other)
        {
            return Value == other?.Value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((IValueType1) obj);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public long SizeOfInBytes()
        {
            // wrong but just an example
            return sizeof(int) + 8;
        }

        public override string ToString()
        {
            return $"{nameof(ValueType1)}-{Value}";
        }
    }

    public interface IValueType1 : IEquatable<IValueType1>
    {
        int Value { get; }

        long SizeOfInBytes();
    }
}