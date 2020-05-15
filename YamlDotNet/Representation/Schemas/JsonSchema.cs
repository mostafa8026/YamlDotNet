using System;
using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Helpers;

namespace YamlDotNet.Representation.Schemas
{
    /// <summary>
    /// Implements the JSON schema: <see href="https://yaml.org/spec/1.2/spec.html#id2803231"/>.
    /// </summary>
    public sealed class JsonSchema : RegexBasedSchema
    {
        internal static readonly NumberFormatInfo NumberFormat = new NumberFormatInfo
        {
            //CurrencyDecimalSeparator = ".",
            //CurrencyGroupSeparator = "_",
            //CurrencyGroupSizes = new[] { 3 },
            //CurrencySymbol = string.Empty,
            //CurrencyDecimalDigits = 99,
            NumberDecimalSeparator = ".",
            NumberGroupSeparator = "_",
            NumberGroupSizes = new[] { 3 },
            NumberDecimalDigits = 99,
            NumberNegativePattern = 1, // -1,234.00
            NegativeSign = "-",
            NaNSymbol = ".nan",
            PositiveInfinitySymbol = ".inf",
            NegativeInfinitySymbol = "-.inf",
        };

        private JsonSchema(IScalarMapper? fallbackTag) : base(BuildMappingTable(), fallbackTag, ScalarStyle.DoubleQuoted, SequenceStyle.Flow, MappingStyle.Flow) { }

        /// <summary>
        /// A version of the <see cref="JsonSchema"/> that conforms strictly to the specification
        /// by not resolving any unrecognized scalars.
        /// </summary>
        public static readonly JsonSchema Strict = new JsonSchema(null);

        /// <summary>
        /// A version of the <see cref="JsonSchema"/> that treats unrecognized scalars as strings.
        /// </summary>
        public static readonly JsonSchema Lenient = new JsonSchema(StringTag.Instance);

        private static RegexTagMappingTable BuildMappingTable() => new RegexTagMappingTable
        {
            {
                "^null$",
                YamlTagRepository.Null,
                s => null,
                _ => "null"
            },
            {
                "^(true|false)$",
                YamlTagRepository.Boolean,
                // Assumes that the value matches the regex
                s => s.Value[0] == 't',
                FormatBoolean
            },
            {
                "^-?(0|[1-9][0-9]*)$",
                YamlTagRepository.Integer,
                s => IntegerParser.ParseBase10(s.Value),
                FormatInteger
            },
            {
                @"^-?(0|[1-9][0-9]*)(\.[0-9]*)?([eE][-+]?[0-9]+)?$",
                YamlTagRepository.FloatingPoint,
                s => FloatingPointParser.ParseBase10Unseparated(s.Value),
                FormatFloatingPoint
            }
        };

        internal static string FormatBoolean(object? native)
        {
            return true.Equals(native) ? "true" : "false";
        }

        internal static string FormatInteger(object? native)
        {
            return Convert.ToString(native, NumberFormat)!;
        }

        internal static string FormatFloatingPoint(object? native)
        {
            switch (native)
            {
                case double doublePrecision:
                    return doublePrecision.ToString("0.0###############", NumberFormat);

                case float singlePrecision:
                    return singlePrecision.ToString("0.0######", NumberFormat);

                default:
                    return Convert.ToString(native, NumberFormat)!;
            }
        }
    }
}
