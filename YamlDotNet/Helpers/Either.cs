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

using System.Diagnostics.CodeAnalysis;

namespace YamlDotNet.Helpers
{
    /// <summary>
    /// Represents a discriminated union of two types.
    /// </summary>
    /// <remarks>
    /// This implementation is not intended for general purpose use.
    /// It makes the following assumptions about the values contained in order to save one reference:
    ///   - Both types must be reference types;
    ///   - None of the types can extend the other;
    ///   - It is a value type, so it is possible to create an invalid instance by using the default constructor.
    /// </remarks>
    /// <typeparam name="TLeft"></typeparam>
    /// <typeparam name="TRight"></typeparam>
    public struct Either<TLeft, TRight>
        where TLeft : class
        where TRight : class
    {
        private readonly object value;

        private Either(object value)
        {
            this.value = value;
        }

        public bool IsEmpty => value is null;

        public bool GetValue([NotNullWhen(true)] out TLeft? left, [NotNullWhen(false)] out TRight? right)
        {
            left = value as TLeft;
            right = value as TRight;
            return right is null;
        }

        public static implicit operator Either<TLeft, TRight>(TLeft left)
        {
            return new Either<TLeft, TRight>(left);
        }

        public static implicit operator Either<TLeft, TRight>(TRight right)
        {
            return new Either<TLeft, TRight>(right);
        }

        public static implicit operator Either<TLeft, TRight>(EitherConverter<TLeft> left) => new Either<TLeft, TRight>(left.value);
        public static implicit operator Either<TLeft, TRight>(EitherConverter<TRight> right) => new Either<TLeft, TRight>(right.value);
    }

    public struct EitherConverter<T>
    {
        public readonly T value;

        public EitherConverter(T value)
        {
            this.value = value;
        }
    }
}
