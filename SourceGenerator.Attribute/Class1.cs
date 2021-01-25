using System;

namespace SourceGenerator.Attribute
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    public class CreateMemoizedImplementationAttribute : System.Attribute
    {
        public string? Name { get; set; }
    }

    internal struct ArgKey : IEquatable<ArgKey>
    {
        private readonly string _method;
        private readonly object?[] _values;

        public ArgKey(string method, object?[] values)
        {
            _method = method;
            _values = values;
        }

        public override bool Equals(object? obj)
        {
            return obj is ArgKey key && Equals(key);
        }

        public bool Equals(ArgKey other)
        {
            if (_method != other._method)
            {
                return false;
            }

            if (_values.Length != other._values.Length)
            {
                return false;
            }

            for (var i = 0; i < _values.Length; ++i)
            {
                if (_values[i] == null && other._values[i] == null)
                {
                    return true;
                }

                if (!(_values[i]?.Equals(other._values[i]) ?? false))
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            var result = 1291433875 + _method.GetHashCode();
            foreach(var o in _values)
            {
                result = (result * 397) ^ o?.GetHashCode() ?? 0;
            }

            return result;
        }

        public static bool operator ==(ArgKey left, ArgKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ArgKey left, ArgKey right)
        {
            return !(left == right);
        }
    }
}