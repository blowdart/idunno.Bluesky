// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Downloads;

/// <summary>Lists the labeler administration CLI builds shipped inside the container image.</summary>
/// <param name="configuration">The application configuration, used to locate the downloads directory.</param>
public sealed class IndexModel(IConfiguration configuration) : PageModel
{
    internal const string ManifestFileName = "cli-downloads.json";

    private readonly string _directory = configuration["Labeler:CliDownloadsDirectory"] is { Length: > 0 } configured
        ? configured
        : "/opt/labeler-downloads";

    /// <summary>Gets the downloads described by the build manifest, or an empty list when the image carries none.</summary>
    internal IReadOnlyList<CliDownload> Downloads { get; private set; } = [];

    /// <summary>Loads the manifest written when the image was built.</summary>
    public void OnGet()
    {
        Response.Headers.CacheControl = "private, no-store";
        Downloads = ReadManifest(_directory);
    }

    /// <summary>Streams a build listed in the manifest. Only manifest names that exist on disk can be requested.</summary>
    /// <param name="name">The file name from the manifest.</param>
    /// <returns>The zip archive, or not found.</returns>
    public IActionResult OnGetFile(string? name)
    {
        Response.Headers.CacheControl = "private, no-store";
        CliDownload? download = ReadManifest(_directory).FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
        if (download is null)
        {
            return NotFound();
        }

        string path = Path.Combine(_directory, download.Name);
        return System.IO.File.Exists(path) ? PhysicalFile(path, "application/zip", download.Name) : NotFound();
    }

    internal static List<CliDownload> ReadManifest(string directory)
    {
        string path = Path.Combine(directory, ManifestFileName);
        List<CliDownload> downloads = [];
        if (!System.IO.File.Exists(path))
        {
            return downloads;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(System.IO.File.ReadAllText(path));
            if (document.RootElement.ValueKind is not JsonValueKind.Array)
            {
                return downloads;
            }

            foreach (JsonElement entry in document.RootElement.EnumerateArray())
            {
                if (entry.ValueKind is not JsonValueKind.Object)
                {
                    continue;
                }

                string? name = Text(entry, "name");
                string? os = Text(entry, "os");
                string? arch = Text(entry, "arch");
                string? sha256 = Text(entry, "sha256");
                string? sha512 = Text(entry, "sha512");
                if (name is null || os is null || arch is null || sha256 is null || sha512 is null ||
                    name != Path.GetFileName(name) || !name.EndsWith(".zip", StringComparison.Ordinal))
                {
                    continue;
                }

                long size = entry.TryGetProperty("size", out JsonElement sizeElement) && sizeElement.ValueKind is JsonValueKind.Number && sizeElement.TryGetInt64(out long parsed) ? parsed : 0;
                downloads.Add(new CliDownload(name, os, arch, Text(entry, "version") ?? string.Empty, size, sha256, sha512));
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            downloads.Clear();
        }

        return downloads;
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind is JsonValueKind.String ? value.GetString() : null;
}

/// <summary>A CLI build listed in the image's download manifest. Checksums come from the manifest, not from hashing at request time.</summary>
internal sealed record CliDownload(string Name, string Os, string Arch, string Version, long Size, string Sha256, string Sha512);
