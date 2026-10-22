#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Generates per target framework CycloneDX and SPDX BOMs for every packable project under src/.
// The BOMs are written to <project>/obj/sbom/<tfm>/ where src/Directory.Build.props packs them into the nupkg.
//
// Usage: dotnet run .build/GenerateSboms.cs -- [--public-release true|false] [--syft <path>] [--root <repo root>]
//
// Requires the repository's local tools (dotnet tool restore) and Syft on the path (or via --syft).

string root = Directory.GetCurrentDirectory();
string syft = "syft";
bool publicRelease = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--root" when i + 1 < args.Length:
            root = Path.GetFullPath(args[++i]);
            break;
        case "--syft" when i + 1 < args.Length:
            syft = args[++i];
            break;
        case "--public-release" when i + 1 < args.Length:
            publicRelease = bool.Parse(args[++i]);
            break;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument '{args[i]}'.");
            return 1;
    }
}

string metadataTemplate = Path.Combine(root, ".build", "sbom-metadata.xml");
string configProperties = $"-p:Configuration=Release -p:PublicRelease={publicRelease.ToString().ToLowerInvariant()}";

string[] projects = [.. Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
    .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
    .OrderBy(p => p, StringComparer.Ordinal)];

// A normal restore first, because the version query needs the Nerdbank.GitVersioning targets to be restored.
Run("dotnet", $"restore \"{Path.Combine(root, "idunno.Bluesky.slnx")}\" {configProperties} --verbosity quiet");

Dictionary<string, ProjectInfo> packable = [];
foreach (string project in projects)
{
    string propertiesJson = Run("dotnet", $"msbuild \"{project}\" -t:GetBuildVersion -getProperty:IsPackable -getProperty:PackageId -getProperty:NuGetPackageVersion -getProperty:TargetFrameworks -p:Configuration=Release -p:PublicRelease={publicRelease.ToString().ToLowerInvariant()} -nologo")
        .Trim();

    JsonNode properties = JsonNode.Parse(propertiesJson)!["Properties"]!;
    if (!string.Equals(properties["IsPackable"]?.GetValue<string>(), "true", StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    string id = properties["PackageId"]!.GetValue<string>();
    if (string.IsNullOrEmpty(id))
    {
        id = Path.GetFileNameWithoutExtension(project);
    }

    packable[id] = new ProjectInfo(
        project,
        id,
        properties["NuGetPackageVersion"]!.GetValue<string>(),
        properties["TargetFrameworks"]!.GetValue<string>().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

Console.WriteLine($"Generating SBOMs for {packable.Count} packages.");

try
{
    foreach (ProjectInfo info in packable.Values)
    {
        string projectDirectory = Path.GetDirectoryName(info.Path)!;
        string sbomRoot = Path.Combine(projectDirectory, "obj", "sbom");
        if (Directory.Exists(sbomRoot))
        {
            Directory.Delete(sbomRoot, recursive: true);
        }

        foreach (string tfm in info.TargetFrameworks)
        {
            Console.WriteLine($"{info.Id} {info.Version} {tfm}");

            // Single framework restore, so the BOM only contains the packages for this target framework.
            Run("dotnet", $"restore \"{info.Path}\" {configProperties} -p:TargetFramework={tfm} --verbosity quiet");

            string outputDirectory = Path.Combine(sbomRoot, tfm);
            Directory.CreateDirectory(outputDirectory);

            Run("dotnet", $"tool run dotnet-CycloneDX \"{info.Path}\" --disable-package-restore -ipr -o \"{outputDirectory}\" -fn bom.cdx.json -F Json -sn \"{info.Id}\" -sv \"{info.Version}\" -st Library --set-nuget-purl -imp \"{metadataTemplate}\"", root);

            string cycloneDxPath = Path.Combine(outputDirectory, "bom.cdx.json");
            JsonNode bom = FilterBom(File.ReadAllText(cycloneDxPath), info, packable);
            File.WriteAllText(cycloneDxPath, bom.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

            Run(syft, $"convert \"{cycloneDxPath}\" -o spdx-json=\"{Path.Combine(outputDirectory, "bom.spdx.json")}\"");

            Validate(outputDirectory, info);
        }
    }
}
finally
{
    // Put obj/project.assets.json back to the multi target version for build, test and pack.
    Run("dotnet", $"restore \"{Path.Combine(root, "idunno.Bluesky.slnx")}\" {configProperties} --verbosity quiet");
}

return 0;

static JsonNode FilterBom(string json, ProjectInfo info, Dictionary<string, ProjectInfo> packable)
{
    // The tool cannot see Nerdbank.GitVersioning versions for project references and reports 1.0.0, so correct them.
    foreach (ProjectInfo referenced in packable.Values)
    {
        json = json.Replace($"pkg:nuget/{referenced.Id}@1.0.0", $"pkg:nuget/{referenced.Id}@{referenced.Version}", StringComparison.Ordinal);
    }

    JsonNode bom = JsonNode.Parse(json)!;
    JsonArray components = bom["components"]?.AsArray() ?? [];

    foreach (JsonNode? component in components)
    {
        if (component?["name"]?.GetValue<string>() is string name &&
            packable.TryGetValue(name, out ProjectInfo? referenced) &&
            component["purl"]?.GetValue<string>() is string purl &&
            purl.EndsWith($"@{referenced.Version}", StringComparison.Ordinal))
        {
            component["version"] = referenced.Version;
        }
    }

    // Remove build only packages (PrivateAssets="All" and the like). SPDX has no scope, so this must happen before conversion.
    HashSet<string> kept = [];
    for (int i = components.Count - 1; i >= 0; i--)
    {
        JsonNode? component = components[i];
        if (component?["scope"]?.GetValue<string>() == "excluded")
        {
            components.RemoveAt(i);
        }
        else if (component?["bom-ref"]?.GetValue<string>() is string reference)
        {
            kept.Add(reference);
        }
    }

    if (bom["metadata"]?["component"]?["bom-ref"]?.GetValue<string>() is string rootReference)
    {
        kept.Add(rootReference);
    }

    if (bom["dependencies"]?.AsArray() is JsonArray dependencies)
    {
        for (int i = dependencies.Count - 1; i >= 0; i--)
        {
            JsonNode? dependency = dependencies[i];
            if (dependency?["ref"]?.GetValue<string>() is not string reference || !kept.Contains(reference))
            {
                dependencies.RemoveAt(i);
                continue;
            }

            if (dependency["dependsOn"]?.AsArray() is JsonArray dependsOn)
            {
                for (int j = dependsOn.Count - 1; j >= 0; j--)
                {
                    if (!kept.Contains(dependsOn[j]!.GetValue<string>()))
                    {
                        dependsOn.RemoveAt(j);
                    }
                }
            }
        }
    }

    return bom;
}

static void Validate(string directory, ProjectInfo info)
{
    JsonNode cycloneDx = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "bom.cdx.json")))!;
    JsonNode spdx = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "bom.spdx.json")))!;

    JsonArray components = cycloneDx["components"]?.AsArray() ?? [];
    List<string> errors = [];

    HashSet<string> references = [.. components.Select(c => c!["bom-ref"]!.GetValue<string>())];
    if (cycloneDx["metadata"]?["component"]?["bom-ref"]?.GetValue<string>() is string rootReference)
    {
        references.Add(rootReference);
    }

    if (components.Any(c => c?["scope"]?.GetValue<string>() == "excluded"))
    {
        errors.Add("CycloneDX BOM contains build only components.");
    }

    string[] duplicates = [.. components.Select(c => c!["bom-ref"]!.GetValue<string>()).GroupBy(r => r).Where(g => g.Count() > 1).Select(g => g.Key)];
    if (duplicates.Length > 0)
    {
        errors.Add($"CycloneDX BOM contains duplicate components: {string.Join(", ", duplicates)}");
    }

    foreach (JsonNode? dependency in cycloneDx["dependencies"]?.AsArray() ?? [])
    {
        IEnumerable<string> refs = [dependency!["ref"]!.GetValue<string>(), .. (dependency["dependsOn"]?.AsArray() ?? []).Select(d => d!.GetValue<string>())];
        foreach (string reference in refs.Where(r => !references.Contains(r)))
        {
            errors.Add($"CycloneDX BOM has a dangling dependency reference: {reference}");
        }
    }

    if (cycloneDx["metadata"]?["component"]?["version"]?.GetValue<string>() != info.Version)
    {
        errors.Add($"CycloneDX root component version is not {info.Version}.");
    }

    if (spdx["packages"]?.AsArray() is not JsonArray { Count: > 0 })
    {
        errors.Add("SPDX BOM contains no packages.");
    }

    if (errors.Count > 0)
    {
        throw new InvalidOperationException($"{info.Id} ({directory}) failed validation:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    }
}

static string Run(string fileName, string arguments, string? workingDirectory = null)
{
    ProcessStartInfo startInfo = new(fileName, arguments)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    if (workingDirectory is not null)
    {
        startInfo.WorkingDirectory = workingDirectory;
    }

    using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
    Task<string> error = process.StandardError.ReadToEndAsync();
    string output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"'{fileName} {arguments}' failed with exit code {process.ExitCode}:{Environment.NewLine}{output}{Environment.NewLine}{error.Result}");
    }

    return output;
}

internal sealed record ProjectInfo(string Path, string Id, string Version, string[] TargetFrameworks);
