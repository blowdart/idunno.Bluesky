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
                  { "name": "cli-linux-x64.zip", "rid": "linux-x64", "os": "linux", "arch": "x64", "size": 10, "sha256": "a", "sha512": "b", "version": "1.0.0" },
                  { "name": "../escape.zip", "os": "linux", "arch": "x64", "size": 1, "sha256": "a", "sha512": "b" },
                  { "name": "tool.exe", "os": "win", "arch": "x64", "size": 1, "sha256": "a", "sha512": "b" },
                  { "name": "missing-hash.zip", "os": "win", "arch": "x64" }
                ]
                """);

            List<CliDownload> downloads = IndexModel.ReadManifest(directory);

            CliDownload download = Assert.Single(downloads);
            Assert.Equal("cli-linux-x64.zip", download.Name);
            Assert.Equal("1.0.0", download.Version);
            Assert.Equal(10, download.Size);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RelativeDownloadDirectoryProducesAbsolutePhysicalFile()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, IndexModel.ManifestFileName), """[{ "name": "cli.zip", "os": "linux", "arch": "x64", "size": 1, "sha256": "a", "sha512": "b" }]""");
            File.WriteAllText(Path.Combine(directory, "cli.zip"), "zip");

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

            PhysicalFileResult result = Assert.IsType<PhysicalFileResult>(model.OnGetFile("cli.zip"));
            Assert.True(Path.IsPathRooted(result.FileName));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("""[null, 5, "text", { "name": "ok.zip", "os": "win", "arch": "x64", "size": 7, "sha256": "a", "sha512": "b" }]""", 7)]
    [InlineData("""[{ "name": "ok.zip", "os": "win", "arch": "x64", "size": "big", "sha256": "a", "sha512": "b" }]""", 0)]
    [InlineData("""[{ "name": "ok.zip", "os": "win", "arch": "x64", "size": null, "sha256": "a", "sha512": "b" }]""", 0)]
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
