// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace idunno.VersionGenerator
{
    [Generator]
    internal sealed class VersioningSourceGenerator : IIncrementalGenerator
    {
        const string VersionFileName = "version.json";

        const string Pattern = @"""version"":(?:\s*)""(?<version>.*)""";

        private static readonly Regex s_versionExtractorRegex = new(
            pattern: Pattern,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            matchTimeout: new TimeSpan(0, 0, 30));

        /// <inheritdoc/>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider <(string name, string code)> pipeline = context.AdditionalTextsProvider
                .Where(static file => Path.GetFileName(file.Path).Equals(VersionFileName, StringComparison.Ordinal))
                .Select(static (text, cancellationToken) =>
                {
                    string name = Path.GetFileName(text.Path);
                    SourceText? jsonText = text.GetText(cancellationToken);

                    if (jsonText is not null)
                    {
                        Match capture = s_versionExtractorRegex.Match(jsonText.ToString());

                        if (capture.Success)
                        {
                            string extractedVersion = capture.Groups["version"].ToString(); ;

                            string code = GenerateVersionClass(extractedVersion);

                            return (name, code);
                        }
                        else
                        {
                            return (name, string.Empty);
                        }
                    }
                    else
                    {
                        return (name, string.Empty);
                    }
                });

            context.RegisterSourceOutput(
                pipeline,
                static (context, pair) =>
                    context.AddSource($"idunno.VersionGenerator.g.cs", SourceText.From(pair.code, Encoding.UTF8)));

        }

        private static string GenerateVersionClass(string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                return $@"
namespace idunno
{{
    internal static class Versioning
    {{
        #warning Version attribute not found in {VersionFileName}
        public const string JsonVersion = ""NotFound"";
    }}
}}";
            }
            else
            {
                return $@"
namespace idunno
{{
    internal static class Versioning
    {{
        public const string JsonVersion = ""{version}"";
    }}
}}";
            }
        }
    }
}
