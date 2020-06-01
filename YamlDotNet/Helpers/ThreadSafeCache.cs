//  This file is part of YamlDotNet - A .NET library for YAML.
//  Copyright (c) Antoine Aubry and contributors

//  Permission is hereby granted, free of charge, to any person obtaining a copy of
//  this software and associated documentation files (the "Software"), to deal in
//  the Software without restriction, including without limitation the rights to
//  use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
//  of the Software, and to permit persons to whom the Software is furnished to do
//  so, subject to the following conditions:

//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.

//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace YamlDotNet.Helpers
{
    internal sealed class ThreadSafeCache<TKey, TValue> : ICache<TKey, TValue>
        where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, Lazy<TValue>> entries = new ConcurrentDictionary<TKey, Lazy<TValue>>();

        public void Add(TKey key, TValue value)
        {
            if (!entries.TryAdd(key, Lazy.FromValue(value)))
            {
                throw new ArgumentException($"An element with the same key '{key}' already exists in the cache");
            }
        }

        public TValue GetOrAdd(TKey key, Func<TValue> valueFactory)
        {
            // Lazy<T> takes care of detecting recursion in the value factory.
            var value = entries.GetOrAdd(key, k => new Lazy<TValue>(() =>
            {
                try
                {
                    return valueFactory();
                }
                catch(InvalidRecursionException ex)
                {
                    ex.AddPath(key.ToString()!);
                    throw;
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidRecursionException(
                        "The valueFactory that was passed to ThreadSafeCache.GetOrAdd() attempted to call itself.",
                        key.ToString()!,
                        ex
                    );
                }
            }, isThreadSafe: true));
            return value.Value;
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (entries.TryGetValue(key, out var lazyValue))
            {
                value = lazyValue.Value;
                return true;
            }

            value = default!;
            return false;
        }
    }
}
