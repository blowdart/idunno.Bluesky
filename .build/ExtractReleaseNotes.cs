#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:package System.CommandLine@2.0.10
#:package SemVer@3.0.0
#:property ManagePackageVersionsCentrally=false
using System.CommandLine;
using System.CommandLine.Parsing;

using Semver;

Option<string> directoryOption = new("--directory", "-d")
{
    Description = "The directory to check for CHANGELOG.md. Defaults to the current working directory.",
    DefaultValueFactory = _ => Directory.GetCurrentDirectory(),
    Required = false
};
directoryOption.Validators.Add(result =>
{
    string? directory = result.GetValue(directoryOption);
    if (string.IsNullOrEmpty(directory))
    {
        result.AddError("Directory cannot be null or empty.");
    }
    else if (!Directory.Exists(directory))
    {
        result.AddError($"Directory '{directory}' does not exist.");
    }
});

Option<string> outputFileOption = new("--output-file", "-o")
{
    Description = "The output file to write the release notes to. Defaults to 'release-notes.md'.",
    DefaultValueFactory = _ => "release-notes.md",
    Required = false
};
outputFileOption.Validators.Add(result =>
{
    string? outputFile = result.GetValue(outputFileOption);
    if (string.IsNullOrEmpty(outputFile))
    {
        result.AddError("Output file cannot be null or empty.");
    }
});

Option<string?> versionOption = new("--releaseVersion", "-rv")
{
    Description = "The version to extract release notes for. Defaults to the current tag.",
    Required = false,
    DefaultValueFactory = _ => GetGitTag()
};
versionOption.Validators.Add(result =>
{
    string? version = result.GetValue(versionOption);
    if (string.IsNullOrEmpty(version))
    {
        result.AddError("--releaseVersion cannot be null or empty.");
    }
    else if (!version.StartsWith('v'))
    {
        result.AddError("--releaseVersion must start with 'v'.");
    }
    else
    {
        if (!SemVersion.TryParse(version[1..], SemVersionStyles.Strict, out SemVersion? tagVersion) || tagVersion is null)
        {
            result.AddError($"--releaseVersion '{version}' is not a valid semantic version.");
        }
    }
});

RootCommand rootCommand = new("Extracts release notes for a given version.")
{
    Options = { directoryOption, outputFileOption, versionOption },
};

ParseResult parseResult = rootCommand.Parse(args);
if (parseResult.Errors.Count > 0)
{
    foreach (ParseError error in parseResult.Errors)
    {
        Console.WriteLine($"❌ {error.Message}");
    }
    Environment.Exit((int)ExitCode.InvalidParameters);
}

if (string.IsNullOrEmpty(parseResult.GetValue(versionOption)))
{
    Console.WriteLine("❌ Version is not specified and could not be determined from the current tag.");
    Environment.Exit((int)ExitCode.InvalidParameters);
}

Console.WriteLine($"➡️ Extracting release notes for {parseResult.GetValue(directoryOption)} {parseResult.GetValue(outputFileOption)} {parseResult.GetValue(versionOption)}...");

int result = await ExtractReleaseNotes(parseResult.GetValue(directoryOption)!, parseResult.GetValue(outputFileOption)!, parseResult.GetValue(versionOption)!);
Environment.Exit(result);

static async Task<int> ExtractReleaseNotes(string directory, string outputFile, string version)
{
    try
    {
        if (version.StartsWith('v'))
        {
            version = version[1..]; // Remove the leading 'v' if present
        }

        string changeLog = Path.Combine(directory, "CHANGELOG.md");
        if (!File.Exists(changeLog))
        {
            Console.WriteLine($"❌ CHANGELOG.md does not exist in '{directory}'");
            return (int)ExitCode.Failure;
        }

        string outputFilePath = Path.Combine(directory, outputFile);

        Console.WriteLine($"➡️ Reading from '{changeLog}'...");
        Console.WriteLine($"➡️ Extracting release notes for version {version} into '{outputFilePath}'...");
        Console.WriteLine($"➡️ Looking for line beginning with '## {version}'...");

        bool releaseNotesFound = false;
        using (var reader = new StreamReader(changeLog))
        {
            string? line;
            bool justStarted = true;
            bool inReleaseNotes = false;

            using (var writer = new StreamWriter(outputFilePath))
            {
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith($"## {version}"))
                    {
                        Console.WriteLine($"✅ Found line beginning with '## {version}'...");
                        inReleaseNotes = true;
                        releaseNotesFound = true;
                        // Skip the line with the version header and start writing from the next line
                        continue;
                    }
                    if (inReleaseNotes)
                    {
                        if (justStarted && string.IsNullOrWhiteSpace(line))
                        {
                            // Skip leading empty lines after the version header
                            continue;
                        }
                        justStarted = false;

                        if (line.StartsWith("## "))
                        {
                            break; // End of release notes for the specified version
                        }

                        await writer.WriteLineAsync(line);
                    }
                }
                writer.Flush();
            }
        }

        if (!releaseNotesFound)
        {
            Console.WriteLine($"❌ No release notes found for version {version}.");
            return (int)ExitCode.Failure;
        }

        Console.WriteLine($"✅ Release notes for version {version} written to '{outputFile}'.");
        return (int)ExitCode.Success;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ An error occurred: {ex.Message}");
        return (int)ExitCode.Failure;
    }
}

static string? GetGitTag()
{
    string? tag = null;

    string? gitHubRef = Environment.GetEnvironmentVariable("GITHUB_REF");
    if (string.IsNullOrEmpty(gitHubRef))
    {
        Console.WriteLine("❌ GITHUB_REF environment variable is not set.");
        return null;
    }
    string? gitHubRefType = Environment.GetEnvironmentVariable("GITHUB_REF_TYPE");
    if (string.IsNullOrEmpty(gitHubRefType))
    {
        Console.WriteLine("❌ GITHUB_REF_TYPE environment variable is not set.");
        return null;
    }

    if (string.Compare(gitHubRefType, "tag", StringComparison.OrdinalIgnoreCase) == 0)
    {
        string tagFromGithubRef = gitHubRef!.Substring("refs/tags/".Length);

        Console.WriteLine("➡️ Checking tag format...");

        if (!tagFromGithubRef.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"❌ Tag name {tagFromGithubRef} does not start with 'v'");
            return null;
        }

        if (!SemVersion.TryParse(tagFromGithubRef[1..], SemVersionStyles.Strict, out SemVersion? tagVersion) || tagVersion is null)
        {
            Console.WriteLine($"❌ Tag name {tagFromGithubRef[1..]} is not a valid semantic version");
            return null;
        }

        if (!tagVersion.IsRelease)
        {
            Console.WriteLine($"❌ Tag {tagFromGithubRef[1..]} is not a release version");
            return null;
        }

        tag = tagFromGithubRef;
        Console.WriteLine($"✅ Found tag: {tag}");
    }
    else
    {
        Console.WriteLine($"❌ GITHUB_REF_TYPE is not a tag: {gitHubRefType}");
    }

    return tag;
}

enum ExitCode : int
{
    Success = 0,
    NoRef = 1,
    NoRefType = 2,
    UnknownRefType = 3,
    InvalidParameters = 98,
    Failure = 99
}
