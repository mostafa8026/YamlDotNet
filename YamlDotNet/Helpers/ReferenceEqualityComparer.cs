using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YamlDotNet.Helpers
{
    public sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static readonly IEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

        private ReferenceEqualityComparer() { }

        bool IEqualityComparer<T>.Equals(T x, T y) => ReferenceEquals(x, y);
        int IEqualityComparer<T>.GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
