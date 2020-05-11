using YamlDotNet.Core;
using YamlDotNet.Helpers;

namespace YamlDotNet.Representation.Schemas
{
    /// <summary>
    /// Implements the Core schema: <see href="https://yaml.org/spec/1.2/spec.html#id2804923"/>.
    /// </summary>
    public sealed class CoreSchema : RegexBasedSchema
    {
        private CoreSchema() : base(BuildMappingTable(), FailsafeSchema.String) { }

        public static readonly CoreSchema Instance = new CoreSchema();

        private static RegexTagMappingTable BuildMappingTable() => new RegexTagMappingTable
        {
            {
                "^(null|Null|NULL|~?)$",
                YamlTagRepository.Null,
                s => null,
                _ => "null"
            },
            {
                "^(true|True|TRUE|false|False|FALSE)$",
                YamlTagRepository.Boolean,
                // Assumes that the value matches the regex
                s =>
                {
                    var firstChar = s.Value[0];
                    return firstChar == 't' || firstChar == 'T';
                },
                JsonSchema.FormatBoolean
            },
            {
                "^[-+]?[0-9]+$", // Base 10
                YamlTagRepository.Integer,
                s => IntegerParser.ParseBase10(s.Value),
                JsonSchema.FormatInteger
            },
            {
                "^0o[0-7]+$", // Base 8
                YamlTagRepository.Integer,
                s => IntegerParser.ParseBase8Unsigned(s.Value),
                JsonSchema.FormatInteger
            },
            {
                "^0x[0-9a-fA-F]+$", // Base 16
                YamlTagRepository.Integer,
                s => IntegerParser.ParseBase16Unsigned(s.Value),
                JsonSchema.FormatInteger
            },
            {
                @"^[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?$",
                YamlTagRepository.FloatingPoint,
                s => FloatingPointParser.ParseBase10Unseparated(s.Value),
                JsonSchema.FormatFloatingPoint
            },
            {
                @"^[-+]?(\.inf|\.Inf|\.INF)$",
                YamlTagRepository.FloatingPoint,
                s => s.Value[0] switch
                {
                    '-' => double.NegativeInfinity,
                    _ => double.PositiveInfinity
                },
                JsonSchema.FormatFloatingPoint
            },
            {
                @"^(\.nan|\.NaN|\.NAN)$",
                YamlTagRepository.FloatingPoint,
                s => double.NaN,
                JsonSchema.FormatFloatingPoint
            },
        };
    }
}
