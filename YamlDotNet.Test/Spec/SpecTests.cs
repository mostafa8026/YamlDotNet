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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace YamlDotNet.Test.Spec
{
    public sealed class SpecTests
    {
        private readonly ITestOutputHelper testOutput;

        public SpecTests(ITestOutputHelper testOutput)
        {
            this.testOutput = testOutput ?? throw new ArgumentNullException(nameof(testOutput));
        }

        private const string DescriptionFilename = "===";
        private const string InputFilename = "in.yaml";
        private const string ExpectedEventFilename = "test.event";
        private const string ErrorFilename = "error";

        private static readonly string specFixtureDirectory = TestFixtureHelper.GetTestFixtureDirectory("YAMLDOTNET_SPEC_SUITE_DIR", "yaml-test-suite");

        private static readonly List<string> ignoredSuites = new List<string>
        {
            // no spec test is ignored as of https://github.com/yaml/yaml-test-suite/commit/053b73a9c12c0cd76da797fdc2ffbd4bb5264c12
        };

        private static readonly List<string> knownFalsePositives = new List<string>
        {
            // no false-positives known as of https://github.com/yaml/yaml-test-suite/commit/bf64436490f3f70a883f19c7cd06057190d9caac
        };

        private static readonly List<string> knownParserDesyncInErrorCases = new List<string>
        {
            "5LLU" // remove 5LLU once https://github.com/yaml/yaml-test-suite/pull/61 is released
        };

        [Theory, MemberData(nameof(GetYamlSpecDataSuites))]
        public void ConformsWithYamlSpec(string name, string description, string inputFile, string expectedEventFile, bool error)
        {
            var expectedResult = File.ReadAllText(expectedEventFile);
            using (var writer = new StringWriter())
            {
                try
                {
                    using (var reader = File.OpenText(inputFile))
                    {
                        ConvertToLibYamlStyleAnnotatedEventStream(reader, writer);
                    }
                }
                catch (Exception ex)
                {
                    testOutput.WriteLine("Exception while parsing (this may be expected):\n" + ex.Message);

                    Assert.True(error, $"Unexpected spec failure ({name}).\n{description}\nExpected:\n{expectedResult}\nActual:\n[Writer Output]\n{writer}\n[Exception]\n{ex}");

                    if (error)
                    {
                        Debug.Assert(!knownFalsePositives.Contains(name), $"Spec test '{name}' passed but present in '{nameof(knownFalsePositives)}' list. Consider removing it from the list.");

                        try
                        {
                            Assert.Equal(expectedResult, writer.ToString(), ignoreLineEndingDifferences: true);
                            Debug.Assert(!knownParserDesyncInErrorCases.Contains(name), $"Spec test '{name}' passed but present in '{nameof(knownParserDesyncInErrorCases)}' list. Consider removing it from the list.");
                        }
                        catch (EqualException)
                        {
                            // In some error cases, YamlDotNet's parser output is in desync with what is expected by the spec.
                            // Throw, if it is not a known case.

                            if (!knownParserDesyncInErrorCases.Contains(name))
                            {
                                throw;
                            }
                        }
                    }

                    return;
                }

                try
                {
                    Assert.Equal(expectedResult, writer.ToString(), ignoreLineEndingDifferences: true);
                    Debug.Assert(!ignoredSuites.Contains(name), $"Spec test '{name}' passed but present in '{nameof(ignoredSuites)}' list. Consider removing it from the list.");
                }
                catch (EqualException)
                {
                    // In some cases, YamlDotNet's parser/scanner is unexpectedly *not* erroring out.
                    // Throw, if it is not a known case.

                    if (!(error && knownFalsePositives.Contains(name)))
                    {
                        throw;
                    }
                }
            }
        }

        private static void ConvertToLibYamlStyleAnnotatedEventStream(TextReader textReader, TextWriter textWriter)
        {
            var parser = new Parser(textReader);

            while (parser.MoveNext())
            {
                switch (parser.Current)
                {
                    case AnchorAlias anchorAlias:
                        textWriter.Write("=ALI *");
                        textWriter.Write(anchorAlias.Value);
                        break;
                    case DocumentEnd documentEnd:
                        textWriter.Write("-DOC");
                        if (!documentEnd.IsImplicit) textWriter.Write(" ...");
                        break;
                    case DocumentStart documentStart:
                        textWriter.Write("+DOC");
                        if (!documentStart.IsImplicit) textWriter.Write(" ---");
                        break;
                    case MappingEnd _:
                        textWriter.Write("-MAP");
                        break;
                    case MappingStart mappingStart:
                        textWriter.Write("+MAP");
                        WriteAnchorAndTag(mappingStart);
                        break;
                    case Scalar scalar:
                        textWriter.Write("=VAL");
                        WriteAnchorAndTag(scalar);

                        switch (scalar.Style)
                        {
                            case ScalarStyle.DoubleQuoted: textWriter.Write(" \""); break;
                            case ScalarStyle.SingleQuoted: textWriter.Write(" '"); break;
                            case ScalarStyle.Folded: textWriter.Write(" >"); break;
                            case ScalarStyle.Literal: textWriter.Write(" |"); break;
                            default: textWriter.Write(" :"); break;
                        }

                        foreach (char character in scalar.Value)
                        {
                            switch (character)
                            {
                                case '\b': textWriter.Write("\\b"); break;
                                case '\t': textWriter.Write("\\t"); break;
                                case '\n': textWriter.Write("\\n"); break;
                                case '\r': textWriter.Write("\\r"); break;
                                case '\\': textWriter.Write("\\\\"); break;
                                default: textWriter.Write(character); break;
                            }
                        }
                        break;
                    case SequenceEnd _:
                        textWriter.Write("-SEQ");
                        break;
                    case SequenceStart sequenceStart:
                        textWriter.Write("+SEQ");
                        WriteAnchorAndTag(sequenceStart);
                        break;
                    case StreamEnd _:
                        textWriter.Write("-STR");
                        break;
                    case StreamStart _:
                        textWriter.Write("+STR");
                        break;
                }
                textWriter.Write('\n');
            }

            void WriteAnchorAndTag(NodeEvent nodeEvent)
            {
                if (!nodeEvent.Anchor.IsEmpty)
                {
                    textWriter.Write(" &");
                    textWriter.Write(nodeEvent.Anchor);
                }

                var tagIsExplicit = !nodeEvent.Tag.IsNonSpecific;
                if (!tagIsExplicit && nodeEvent is Scalar scalar && scalar.Style == ScalarStyle.Plain)
                {
                    tagIsExplicit = !scalar.Tag.IsEmpty;
                }

                if (tagIsExplicit)
                {
                    textWriter.Write(" <");
                    textWriter.Write(nodeEvent.Tag.Value);
                    textWriter.Write(">");
                }
            }
        }

        public static IEnumerable<object[]> GetYamlSpecDataSuites()
        {
            var fixtures = Directory.EnumerateDirectories(specFixtureDirectory, "*", SearchOption.TopDirectoryOnly);

            foreach (var testPath in fixtures)
            {
                var testName = Path.GetFileName(testPath);
                // comment the following line to run spec tests (requires 'Rebuild')
                if (ignoredSuites.Contains(testName)) continue;

                var inputFile = Path.Combine(testPath, InputFilename);
                if (!File.Exists(inputFile)) continue;

                var descriptionFile = Path.Combine(testPath, DescriptionFilename);
                var hasErrorFile = File.Exists(Path.Combine(testPath, ErrorFilename));
                var expectedEventFile = Path.Combine(testPath, ExpectedEventFilename);

                yield return new object[]
                {
                    testName,
                    File.ReadAllText(descriptionFile).TrimEnd(),
                    inputFile,
                    expectedEventFile,
                    hasErrorFile
                };
            }
        }
    }
}
