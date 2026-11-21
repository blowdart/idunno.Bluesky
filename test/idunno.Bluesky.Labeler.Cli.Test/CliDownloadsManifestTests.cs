// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Downloads;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>Covers the CLI downloads manifest reader used by the Xenon CLI Downloads page.</summary>
public class CliDownloadsManifestTests
{
    [Fact]
    public void ManifestEntriesAreReadAndUnsafeEntriesAreSkipped()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, IndexModel.ManifestFileName), """
                [
                  { "name": "idunno.Bluesky.Labeler.Cli-linux-x64", "rid": "linux-x64", "os": "linux", "arch": "x64", "size": 10, "sha256": "a", "sha512": "b", "version": "1.0.0" },
                  { "name": "../idunno.Bluesky.Labeler.Cli-linux-x64", "os": "linux", "arch": "x64", "size": 1, "sha256": "a", "sha512": "b" },
                  { "name": "..\\idunno.Bluesky.Labeler.Cli-linux-x64", "os": "linux", "arch": "x64", "size": 1, "sha256": "a", "sha512": "b" },
                  { "name": "tool.exe", "os": "win", "arch": "x64", "size": 1, "sha256": "a", "sha512": "b" },
                  { "name": "idunno.Bluesky.Labeler.Cli-win-x64.exe", "os": "win", "arch": "x64" },
                  { "name": "idunno.Bluesky.Labeler.Cli-linux-x64.zip", "os": "linux", "arch": "x64", "size": 1, "sha256": "a", "sha512": "b" },
                  { "name": "idunno.Bluesky.Labeler.Cli-win-x64.exe", "os": "linux", "arch": "x64", "size": 1, "sha256": "a", "sha512": "b" },
                  { "name": "idunno.Bluesky.Labeler.Cli-linux-other", "os": "linux", "arch": "other", "size": 1, "sha256": "a", "sha512": "b" }
                ]
                """);

            List<CliDownload> downloads = IndexModel.ReadManifest(directory);

            CliDownload download = Assert.Single(downloads);
            Assert.Equal("idunno.Bluesky.Labeler.Cli-linux-x64", download.Name);
            Assert.Equal("1.0.0", download.Version);
            Assert.Equal(10, download.Size);
            Assert.False(download.LocalBuild);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ManifestSkipsMalformedLocalBuildFlags()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, IndexModel.ManifestFileName),
                """[{ "name": "idunno.Bluesky.Labeler.Cli-win-x64.exe", "os": "win", "arch": "x64", "size": 1, "sha256": "a", "sha512": "b", "localBuild": "invalid" }]""");

            Assert.Empty(IndexModel.ReadManifest(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("win", "x64", "idunno.Bluesky.Labeler.Cli-win-x64.exe")]
    [InlineData("win", "arm64", "idunno.Bluesky.Labeler.Cli-win-arm64.exe")]
    [InlineData("linux", "x64", "idunno.Bluesky.Labeler.Cli-linux-x64")]
    [InlineData("linux", "arm64", "idunno.Bluesky.Labeler.Cli-linux-arm64")]
    public void RelativeDownloadDirectoryServesOnlyManifestBinaries(string os, string arch, string name)
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, IndexModel.ManifestFileName),
                $$"""[{ "name": "{{name}}", "os": "{{os}}", "arch": "{{arch}}", "size": 1, "sha256": "a", "sha512": "b" }]""");
            byte[] binary = [0x4d, 0x5a, 0, 0xff];
            File.WriteAllBytes(Path.Combine(directory, name), binary);
            File.WriteAllText(Path.Combine(directory, "settings.json"), "not a download");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Labeler:CliDownloadsDirectory"] = Path.GetRelativePath(Environment.CurrentDirectory, directory)
                })
                .Build();

            IndexModel model = new(configuration)
            {
                PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
            };

            PhysicalFileResult result = Assert.IsType<PhysicalFileResult>(model.OnGetFile(name));
            Assert.True(Path.IsPathRooted(result.FileName));
            Assert.Equal(name, result.FileDownloadName);
            Assert.Equal("application/octet-stream", result.ContentType);
            Assert.Equal(binary, File.ReadAllBytes(result.FileName));
            Assert.IsType<NotFoundResult>(model.OnGetFile("settings.json"));
            File.Delete(Path.Combine(directory, name));
            Assert.IsType<NotFoundResult>(model.OnGetFile(name));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("""[null, 5, "text", { "name": "idunno.Bluesky.Labeler.Cli-win-x64.exe", "os": "win", "arch": "x64", "size": 7, "sha256": "a", "sha512": "b" }]""", 7)]
    [InlineData("""[{ "name": "idunno.Bluesky.Labeler.Cli-win-x64.exe", "os": "win", "arch": "x64", "size": "big", "sha256": "a", "sha512": "b" }]""", 0)]
    [InlineData("""[{ "name": "idunno.Bluesky.Labeler.Cli-win-x64.exe", "os": "win", "arch": "x64", "size": null, "sha256": "a", "sha512": "b" }]""", 0)]
    public void MalformedEntriesAndSizesDoNotThrow(string content, long expectedSize)
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, IndexModel.ManifestFileName), content);

            CliDownload download = Assert.Single(IndexModel.ReadManifest(directory));
            Assert.Equal(expectedSize, download.Size);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("{}")]
    public void MissingOrInvalidManifestsProduceNoDownloads(string? content)
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            if (content is not null)
            {
                File.WriteAllText(Path.Combine(directory, IndexModel.ManifestFileName), content);
            }

            Assert.Empty(IndexModel.ReadManifest(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
