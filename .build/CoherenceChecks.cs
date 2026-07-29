#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:package System.CommandLine@2.0.10
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

RootCommand rootCommand = new("Checks the coherence of the repository in preparation for a release or pre-release.")
{
    Options = { directoryOption },
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

int result = await CheckCoherenceAsync(parseResult.GetValue(directoryOption)!);
Environment.Exit(result);

static async Task<int> CheckCoherenceAsync(string directory)
{
    Console.WriteLine("➡️ Checking GitHub Environment...");

    ExitCode exitCode = ExitCode.Success;
    string? gitHubRef = Environment.GetEnvironmentVariable("GITHUB_REF");
    if (string.IsNullOrEmpty(gitHubRef))
    {
        Console.WriteLine("❌ GITHUB_REF environment variable is not set. Cannot check coherence");
        exitCode = ExitCode.NoRef;
    }
    string? gitHubRefType = Environment.GetEnvironmentVariable("GITHUB_REF_TYPE");
    if (string.IsNullOrEmpty(gitHubRefType))
    {
        Console.WriteLine("❌ GITHUB_REF_TYPE environment variable is not set. Cannot check coherence");
        exitCode = ExitCode.NoRefType;
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
                Console.WriteLine("❌ Failed to get version from version.json");
                return (int)ExitCode.MissingVersionJson;
            }

            if (!version.IsRelease)
            {
                Console.WriteLine($"❌ version.json does not contain a release version {version}");
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
                Console.WriteLine("❌ Failed to get version from version.json");
                return (int)ExitCode.MissingVersionJson;
            }
            Console.WriteLine($"➡️ version.json version is {version}");

            if (version.Prerelease == null)
            {
                Console.WriteLine($"❌ {version} in version.json version has no prerelease tag.");
                return (int)ExitCode.NotPrereleaseVersion;
            }
            else if (!version.Prerelease.Equals("prerelease", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"❌ {version} in version.json version has incorrect prerelease tag.");
                return (int)ExitCode.NotPrereleaseTag;
            }
            Console.WriteLine($"✔️ Prerelease version");

            if (branchName.StartsWith("version/v", StringComparison.OrdinalIgnoreCase))
            {
                if (branchName.Length <= "version/v".Length)
                {
                    Console.WriteLine($"❌ Branch name {branchName} does not begin with 'version/v'");
                    return (int)ExitCode.VersionBranchMissingPrefix;
                }
                SemVersion? branchVersion = SemVersion.Parse(branchName.Substring("version/v".Length), SemVersionStyles.Strict);
                if (!branchVersion.Major.Equals(version.Major) || !branchVersion.Minor.Equals(version.Minor) || !branchVersion.Patch.Equals(version.Patch))
                {
                    Console.WriteLine($"version.json version {version} does not match version from branch {branchVersion}");
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
                Console.WriteLine($"❌ {branchName} is not a version branch. Version branches must start with 'version/v'");
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
            Console.WriteLine($"❌ Tag name {tag} does not start with 'v'");
            return (int)ExitCode.TagMissingPrefix;
        }
        Console.WriteLine($"✔️ Tag name {tag} is a version tag");

        if (!SemVersion.TryParse(tag.Substring(1), SemVersionStyles.Strict, out SemVersion? tagVersion) || tagVersion is null)
        {
            Console.WriteLine($"❌ Tag name {tag} is not a valid semantic version");
            return (int)ExitCode.TagInvalid;
        }

        if (!tagVersion.IsRelease)
        {
            Console.WriteLine($"❌ Tag {tag} is not a release version");
            return (int)ExitCode.NotReleaseVersion;
        }

        // Check the CHANGELOG.md has an entry for the json version that matches the tag
        SemVersion? releaseJsonVersion = await GetReleaseJsonVersionAsync(dirInfo);
        if (releaseJsonVersion is null)
        {
            Console.WriteLine("❌ Failed to get version from version.json");
            return (int)ExitCode.MissingVersionJson;
        }

        if (!tagVersion.Equals(releaseJsonVersion))
        {
            Console.WriteLine($"❌ Tag version {tagVersion} does not match version.json version {releaseJsonVersion}");
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
        Console.WriteLine($"❌ GITHUB_REF_TYPE is not a branch or tag: {gitHubRefType}");
        return (int)ExitCode.UnknownRefType;
    }
}

static async Task<SemVersion?> GetReleaseJsonVersionAsync(DirectoryInfo directory)
{
    string versionJsonPath = Path.Combine(directory.FullName, "version.json");
    if (!File.Exists(versionJsonPath))
    {
        Console.WriteLine($"❌ version.json file not found at {versionJsonPath}");
        return null;
    }
    try
    {
        using FileStream fs = new(versionJsonPath, FileMode.Open, FileAccess.Read);
        VersionJson? versionFile = await JsonSerializer.DeserializeAsync<VersionJson>(fs, JsonContext.Default.VersionJson);
        if (versionFile == null || string.IsNullOrEmpty(versionFile.Version))
        {
            Console.WriteLine("❌ Version not found in version.json");
            return null;
        }

        if (SemVersion.TryParse(versionFile.Version, SemVersionStyles.Strict, out SemVersion? parsedVersion) && parsedVersion is not null)
        {
            return parsedVersion;
        }
        else
        {
            Console.WriteLine($"❌ Invalid version format in version.json: {versionFile.Version}");
            return null;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error reading version.json: {ex.Message}");
        return null;
    }
}

static async Task<bool> CheckChangelogForVersionAsync(DirectoryInfo directory, SemVersion version)
{
    string changelogPath = Path.Combine(directory.FullName, "CHANGELOG.md");
    if (!File.Exists(changelogPath))
    {
        Console.WriteLine($"❌ CHANGELOG.md file not found at {changelogPath}");
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

    Console.WriteLine($"❌ Version heading for {version} not found in CHANGELOG.md");
    return false;
}

static async Task<bool> CheckChangelogForVersionAndReleaseDateAsync(DirectoryInfo directory, SemVersion version)
{
    string changelogPath = Path.Combine(directory.FullName, "CHANGELOG.md");
    if (!File.Exists(changelogPath))
    {
        Console.WriteLine($"❌ CHANGELOG.md file not found at {changelogPath}");
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
                Console.WriteLine($"❌ CHANGELOG.MD version heading for {version} found at line {lineNumber} but release date is missing or invalid.");
                return false;
            }
        }
        lineNumber++;
    }

    Console.WriteLine($"❌ Version heading for {version} not found in CHANGELOG.md");
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
            Console.WriteLine($"❌ {file} contains unshipped API changes.");
        }
        return false;
    }

    return true;
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
    InvalidParameters = 98,
    Failure = 99
}
