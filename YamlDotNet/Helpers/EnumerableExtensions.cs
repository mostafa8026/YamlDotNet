using System;
using System.Collections;
using System.Collections.Generic;

namespace YamlDotNet.Helpers
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Wraps an <see cref="IEnumerable{T}" /> to ensure that it can only be enumerated once.
        /// </summary>
        public static IEnumerable<T> SingleUse<T>(this IEnumerable<T> sequence)
        {
            return new SingleUseEnumerable<T>(sequence);
        }

        private sealed class SingleUseEnumerable<T> : IEnumerable<T>
        {
            private IEnumerable<T>? sequence;

            public SingleUseEnumerable(IEnumerable<T> sequence)
            {
                this.sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            }

            public IEnumerator<T> GetEnumerator()
            {
                if (sequence == null)
                {
                    throw new InvalidOperationException("This sequence cannot be enumerated more than once");
                }
                var enumerator = this.sequence.GetEnumerator();
                this.sequence = null;
                return enumerator;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
