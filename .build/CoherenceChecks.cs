#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:package System.CommandLine@2.0.11
#:package SemVer@3.0.0
#:property ManagePackageVersionsCentrally=false
using System.Text.Json;
using System.Text.Json.Serialization;
using System.CommandLine;
using System.CommandLine.Parsing;

using Semver;

Option<string> directoryOption = new("--directory", "-d")
{
    Description = "The directory to check coherence in. Defaults to the current working directory.",
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

Option<string> gitHubRefOption = new("--github-ref", "-ref", "-r")
{
    Description = "The GitHub reference to check coherence against. Defaults to the GITHUB_REF environment variable.",
    DefaultValueFactory = _ => Environment.GetEnvironmentVariable("GITHUB_REF") ?? string.Empty,
    Required = false
};
gitHubRefOption.Validators.Add(result =>
{
    string? gitHubRef = result.GetValue(gitHubRefOption);
    if (string.IsNullOrEmpty(gitHubRef))
    {
        result.AddError("GitHub reference cannot be null or empty.");
    }
});

Option<string> gitHubRefTypeOption = new("--github-ref-type", "-type", "-t", "-rt")
{
    Description = "The GitHub reference type to check coherence against. Defaults to the GITHUB_REF_TYPE environment variable.",
    DefaultValueFactory = _ => Environment.GetEnvironmentVariable("GITHUB_REF_TYPE") ?? string.Empty,
    Required = false
};
gitHubRefTypeOption.Validators.Add(result =>
{
    string? gitHubRefType = result.GetValue(gitHubRefTypeOption);
    if (string.IsNullOrEmpty(gitHubRefType))
    {
        result.AddError("GitHub reference type cannot be null or empty.");
    }
});

Option<string> gitHubWorkflowOption = new("--github-workflow", "-workflow", "-w")
{
    Description = "The GitHub workflow to check coherence against. Defaults to the GITHUB_WORKFLOW environment variable.",
    DefaultValueFactory = _ => Environment.GetEnvironmentVariable("GITHUB_WORKFLOW") ?? string.Empty,
    Required = false
};
gitHubWorkflowOption.Validators.Add(result =>
{
    string? gitHubWorkflow = result.GetValue(gitHubWorkflowOption);
    if (string.IsNullOrEmpty(gitHubWorkflow))
    {
        result.AddError("GitHub workflow cannot be null or empty.");
    }
});

RootCommand rootCommand = new("Checks the coherence of the repository in preparation for a release or pre-release.")
{
    Options = { directoryOption, gitHubRefOption, gitHubRefTypeOption, gitHubWorkflowOption },
};

ParseResult parseResult = rootCommand.Parse(args);
if (parseResult.Errors.Count > 0)
{
    rootCommand.Parse("-h").Invoke();

    foreach (ParseError error in parseResult.Errors)
    {
        WriteError(error.Message);
    }
    Environment.Exit((int)ExitCode.InvalidParameters);
}

int result = await CheckCoherenceAsync(parseResult.GetValue(directoryOption)!, parseResult.GetValue(gitHubRefOption)!, parseResult.GetValue(gitHubRefTypeOption)!, parseResult.GetValue(gitHubWorkflowOption)!);
Environment.Exit(result);

static async Task<int> CheckCoherenceAsync(string directory, string gitHubRef, string gitHubRefType, string gitHubWorkflow)
{
    Console.WriteLine("➡️ Checking GitHub Environment...");

    ExitCode exitCode = ExitCode.Success;
    if (string.IsNullOrEmpty(gitHubRef))
    {
        WriteError("GitHub reference is not provided. Cannot check coherence");
        exitCode = ExitCode.NoRef;
    }
    if (string.IsNullOrEmpty(gitHubRefType))
    {
        WriteError("GitHub reference type is not provided. Cannot check coherence");
        exitCode = ExitCode.NoRefType;
    }
    if (string.IsNullOrEmpty(gitHubWorkflow))
    {
        WriteError("GitHub workflow is not provided. Cannot check coherence");
        exitCode = ExitCode.NoWorkflow;
    }
    if (!string.IsNullOrEmpty(gitHubWorkflow))
    {
        string branchName = gitHubRef.Substring("refs/heads/".Length);

        switch (gitHubWorkflow.ToLowerInvariant())
        {
            case "release":
                Console.WriteLine("➡️ Checking release coherence...");
                if (string.Compare(gitHubRefType, "tag", StringComparison.OrdinalIgnoreCase) != 0)
                {
                    WriteError($"Release workflow needs to be triggered by a tag, but was triggered by a {gitHubRefType}");
                    exitCode = ExitCode.NotBranch;
                }

                if (string.Compare(branchName, "main", StringComparison.OrdinalIgnoreCase) != 0)
                {
                    WriteError($"Release workflow needs to be run on the main branch, but was run on {branchName}");
                    exitCode = ExitCode.NotMainBranch;
                }
                break;

            case "prerelease":
                Console.WriteLine("➡️ Checking prerelease coherence...");
                if (string.Compare(gitHubRefType, "branch", StringComparison.OrdinalIgnoreCase) != 0)
                {
                    WriteError($"Prerelease workflow needs to be run on a branch, but was triggered by a {gitHubRefType}");
                    exitCode = ExitCode.NotBranch;
                }

                if (!branchName.StartsWith("version/v", StringComparison.OrdinalIgnoreCase))
                {
                    WriteError($"Prerelease workflow needs to be a version/v branch but was run on {branchName}");
                    exitCode = ExitCode.NotPrerelease;
                }

                break;

            default:
                WriteError($"Unexpected GitHub workflow: {gitHubWorkflow}");
                exitCode = ExitCode.WorkflowNotKnown;
                break;
        }
    }

    if (exitCode != ExitCode.Success)
    {
        return (int)exitCode;
    }

    DirectoryInfo dirInfo = new(directory);
    Console.WriteLine($"➡️ Checking {dirInfo.FullName}...");

    if (string.Compare(gitHubRefType, "branch", StringComparison.OrdinalIgnoreCase) == 0)
    {
        // Check if the branch is main or a dev branch
        if (gitHubRef!.Equals("refs/heads/main"))
        {
            Console.WriteLine("➡️ Checking release branch coherence...");

            // Check version.json version is a release semantic version
            SemVersion? version = await GetReleaseJsonVersionAsync(dirInfo);
            if (version is null)
            {
                WriteError("Failed to get version from version.json");
                return (int)ExitCode.MissingVersionJson;
            }

            if (!version.IsRelease)
            {
                WriteError($"version.json does not contain a release version {version}");
                return (int)ExitCode.NotReleaseVersion;
            }
            Console.WriteLine($"✔️ version.json has a release version {version}");

            // Check the CHANGELOG.md has an entry for the json version
            if (!await CheckChangelogForVersionAsync(dirInfo, version))
            {
                return (int)ExitCode.ChangelogMissingVersion;
            }
            Console.WriteLine($"✔️ CHANGELOG.md has an entry for version {version}");

            // Check the CHANGELOG.md has an entry for the json version with a release date
            if (!await CheckChangelogForVersionAndReleaseDateAsync(dirInfo, version))
            {
                return (int)ExitCode.ChangelogInvalidReleaseDate;
            }
            Console.WriteLine($"✔️ CHANGELOG.md has a release date for version {version}");

            Console.WriteLine("🎉 release branch coherency checks passed.");
            return (int)ExitCode.Success;
        }
        else
        {
            string branchName = gitHubRef.Substring("refs/heads/".Length);

            Console.WriteLine($"➡️ Checking dev branch coherence on {branchName}...");

            // Check version.json version is a non-release version
            SemVersion? version = await GetReleaseJsonVersionAsync(dirInfo);
            if (version is null)
            {
                WriteError("Failed to get version from version.json");
                return (int)ExitCode.MissingVersionJson;
            }
            Console.WriteLine($"➡️ version.json version is {version}");

            if (version.Prerelease == null)
            {
                WriteError($" {version} in version.json version has no prerelease tag.");
                return (int)ExitCode.NotPrereleaseVersion;
            }
            else if (!version.Prerelease.Equals("prerelease", StringComparison.OrdinalIgnoreCase))
            {
                WriteError($" {version} in version.json version has incorrect prerelease tag.");
                return (int)ExitCode.NotPrereleaseTag;
            }
            Console.WriteLine($"✔️ Prerelease version");

            if (branchName.StartsWith("version/v", StringComparison.OrdinalIgnoreCase))
            {
                if (branchName.Length <= "version/v".Length)
                {
                    WriteError($"Branch name {branchName} does not begin with 'version/v'");
                    return (int)ExitCode.VersionBranchMissingPrefix;
                }
                SemVersion? branchVersion = SemVersion.Parse(branchName.Substring("version/v".Length), SemVersionStyles.Strict);
                if (!branchVersion.Major.Equals(version.Major) || !branchVersion.Minor.Equals(version.Minor) || !branchVersion.Patch.Equals(version.Patch))
                {
                    WriteError($"version.json version {version} does not match version from branch {branchVersion}");
                    return (int)ExitCode.VersionBranchMismatch;
                }
                Console.WriteLine($"✔️ version.json version {version} matches version from branch {branchVersion}");
                if (!await CheckChangelogForVersionAsync(dirInfo,branchVersion))
                {
                    return (int)ExitCode.ChangelogMissingVersion;
                }
                Console.WriteLine($"✔️ CHANGELOG.md has an entry for version {branchVersion}");

                Console.WriteLine("🎉 Version branch coherency checks passed.");
                return (int)ExitCode.Success;
            }
            else
            {
                WriteError($" {branchName} is not a version branch. Version branches must start with 'version/v'");
                return (int)ExitCode.VersionBranchMissingPrefix;
            }
        }
    }
    else if (string.Compare(gitHubRefType, "tag", StringComparison.OrdinalIgnoreCase) == 0)
    {
        string tag = gitHubRef!.Substring("refs/tags/".Length);

        Console.WriteLine("➡️ Checking tag coherence...");

        if (!tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            WriteError($"Tag name {tag} does not start with 'v'");
            return (int)ExitCode.TagMissingPrefix;
        }
        Console.WriteLine($"✔️ Tag name {tag} is a version tag");

        if (!SemVersion.TryParse(tag.Substring(1), SemVersionStyles.Strict, out SemVersion? tagVersion) || tagVersion is null)
        {
            WriteError($"Tag name {tag} is not a valid semantic version");
            return (int)ExitCode.TagInvalid;
        }

        if (!tagVersion.IsRelease)
        {
            WriteError($"Tag {tag} is not a release version");
            return (int)ExitCode.NotReleaseVersion;
        }

        // Check the CHANGELOG.md has an entry for the json version that matches the tag
        SemVersion? releaseJsonVersion = await GetReleaseJsonVersionAsync(dirInfo);
        if (releaseJsonVersion is null)
        {
            WriteError("Failed to get version from version.json");
            return (int)ExitCode.MissingVersionJson;
        }

        if (!tagVersion.Equals(releaseJsonVersion))
        {
            WriteError($"Tag version {tagVersion} does not match version.json version {releaseJsonVersion}");
            return (int)ExitCode.VersionTagMismatch;
        }

        Console.WriteLine($"✔️ Tag version matches version.json");

        // Check the CHANGELOG.md has an entry for the tag version
        if (!await CheckChangelogForVersionAsync(dirInfo, tagVersion))
        {
            return (int)ExitCode.ChangelogMissingVersion;
        }
        Console.WriteLine($"✔️ CHANGELOG.md has an entry for version {tagVersion}");

        // Check the CHANGELOG.md has an entry for the tag json version with a release date
        if (!await CheckChangelogForVersionAndReleaseDateAsync(dirInfo, tagVersion))
        {
            return (int)ExitCode.ChangelogInvalidReleaseDate;
        }
        Console.WriteLine($"✔️ CHANGELOG.md has a release date for version {tagVersion}");

        if (!await CheckPublicAPIUnshippedAsync(dirInfo))
        {
            return (int)ExitCode.PublicAPIsHaveUnshipped;
        }
        Console.WriteLine($"✔️ PublicAPI.unshipped files contain no unshipped APIs");

        Console.WriteLine("🎉 Release coherency checks passed.");
        return (int)ExitCode.Success;
    }
    else
    {
        WriteError($"GITHUB_REF_TYPE is not a branch or tag: {gitHubRefType}");
        return (int)ExitCode.UnknownRefType;
    }
}

static async Task<SemVersion?> GetReleaseJsonVersionAsync(DirectoryInfo directory)
{
    string versionJsonPath = Path.Combine(directory.FullName, "version.json");
    if (!File.Exists(versionJsonPath))
    {
        WriteError($"version.json file not found at {versionJsonPath}");
        return null;
    }
    try
    {
        using FileStream fs = new(versionJsonPath, FileMode.Open, FileAccess.Read);
        VersionJson? versionFile = await JsonSerializer.DeserializeAsync<VersionJson>(fs, JsonContext.Default.VersionJson);
        if (versionFile == null || string.IsNullOrEmpty(versionFile.Version))
        {
            WriteError("Version not found in version.json");
            return null;
        }

        if (SemVersion.TryParse(versionFile.Version, SemVersionStyles.Strict, out SemVersion? parsedVersion) && parsedVersion is not null)
        {
            return parsedVersion;
        }
        else
        {
            WriteError($"Invalid version format in version.json: {versionFile.Version}");
            return null;
        }
    }
    catch (Exception ex)
    {
        WriteError($"Error reading version.json: {ex.Message}");
        return null;
    }
}

static async Task<bool> CheckChangelogForVersionAsync(DirectoryInfo directory, SemVersion version)
{
    string changelogPath = Path.Combine(directory.FullName, "CHANGELOG.md");
    if (!File.Exists(changelogPath))
    {
        WriteError($"CHANGELOG.md file not found at {changelogPath}");
        return false;
    }

    string[] changelogLines = await File.ReadAllLinesAsync(changelogPath);
    string versionHeader = $"## {version.Major}.{version.Minor}.{version.Patch} - ";

    foreach (string line in changelogLines)
    {
        if (line.StartsWith(versionHeader, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    WriteError($"Version heading for {version} not found in CHANGELOG.md");
    return false;
}

static async Task<bool> CheckChangelogForVersionAndReleaseDateAsync(DirectoryInfo directory, SemVersion version)
{
    string changelogPath = Path.Combine(directory.FullName, "CHANGELOG.md");
    if (!File.Exists(changelogPath))
    {
        WriteError($"CHANGELOG.md file not found at {changelogPath}");
        return false;
    }

    string[] changelogLines = await File.ReadAllLinesAsync(changelogPath);
    string versionHeader = $"## {version.Major}.{version.Minor}.{version.Patch} - ";

    int lineNumber = 0;
    foreach (string line in changelogLines)
    {
        if (line.StartsWith(versionHeader, StringComparison.OrdinalIgnoreCase))
        {
            string releaseDateString = line.Substring(versionHeader.Length).Trim();
            if (DateTime.TryParseExact(releaseDateString, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime releaseDate))
            {
                return true;
            }
            else
            {
                WriteError($"CHANGELOG.MD version heading for {version} found at line {lineNumber} but release date is missing or invalid.");
                return false;
            }
        }
        lineNumber++;
    }

    WriteError($"Version heading for {version} not found in CHANGELOG.md");
    return false;
}

static async Task<bool> CheckPublicAPIUnshippedAsync(DirectoryInfo directory)
{
    const string EmptyUnshippedContent = "#nullable enable";
    List<string> nonEmptyFiles = [];

    string[] files = Directory.GetFiles(directory.FullName, "PublicAPI.Unshipped.txt", SearchOption.AllDirectories);
    foreach (string file in files)
    {
        string content = await File.ReadAllTextAsync(file);
        if (!string.Equals(content.Trim(), EmptyUnshippedContent, StringComparison.Ordinal))
        {
            nonEmptyFiles.Add(file.Replace(Directory.GetCurrentDirectory(), "").TrimStart(Path.DirectorySeparatorChar));
        }
    }

    if (nonEmptyFiles.Count > 0)
    {
        foreach (string file in nonEmptyFiles)
        {
            WriteError($" {file} contains unshipped API changes.");
        }
        return false;
    }

    return true;
}

 static void WriteError(string s)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"❌ {s}");
    Console.ResetColor();
}


public record VersionJson([field: JsonRequired] string Version);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(VersionJson))]
partial class JsonContext : JsonSerializerContext
{
}

enum ExitCode : int
{
    Success = 0,
    NoRef = 1,
    NoRefType = 2,
    UnknownRefType = 3,
    MissingVersionJson = 4,
    NotReleaseVersion = 5,
    NotPrereleaseVersion = 6,
    NotPrereleaseTag = 7,
    VersionBranchMismatch = 8,
    VersionBranchMissingPrefix = 9,
    ChangelogMissingVersion = 10,
    ChangelogInvalidReleaseDate = 11,
    TagMissingPrefix = 12,
    TagInvalid = 13,
    VersionTagMismatch = 14,
    PublicAPIsHaveUnshipped = 15,
    NoWorkflow = 16,
    WorkflowNotKnown = 17,
    NotBranch = 18,
    NotPrerelease = 19,
    NotMainBranch = 20,
    InvalidParameters = 98,
    Failure = 99
}
