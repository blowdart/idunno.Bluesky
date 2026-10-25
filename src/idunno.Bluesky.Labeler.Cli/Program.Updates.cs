// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace idunno.Bluesky.Labeler.Cli;

internal static partial class Program
{
    private const string GitHubRepository = "blowdart/idunno.Bluesky";
    private const string LabelerImageRepository = "ghcr.io/blowdart/idunno-bluesky-labeler";
    private const int MaximumReleaseArchiveSize = 512 * 1024 * 1024;

    /// <summary>Creates an explicitly confirmed, provenance-verified self-update command.</summary>
    private static Command CreateCliUpdateCommand()
    {
        Command command = new("update", "Verify and stage a self-contained CLI release update.");
        Option<string> tag = new("--tag") { Description = "The exact stable release tag, such as v9.0.1.", Required = true };
        Option<bool> yes = new("--yes") { Description = "Confirm without an interactive prompt." };
        command.Options.Add(tag);
        command.Options.Add(yes);
        command.SetAction((parseResult, cancellationToken) => ExecuteCliUpdateAsync(
            parseResult.GetValue(tag),
            parseResult.GetValue(yes),
            cancellationToken));
        return command;
    }

    /// <summary>Creates an explicitly confirmed Docker Compose image redeployment command.</summary>
    private static Command CreateServiceUpdateCommand()
    {
        Command command = new("service-update", "Redeploy a labeler image through Docker Compose and verify readiness.");
        Option<FileInfo> composeFile = new("--compose-file") { Description = "The existing Compose file.", Required = true };
        Option<string> service = new("--service") { Description = "The Compose service name for the labeler.", Required = true };
        Option<string> image = new("--image") { Description = "The new public GHCR image pinned by sha256 digest.", Required = true };
        Option<string> previousImage = new("--previous-image") { Description = "The currently deployed image pinned by sha256 digest for rollback.", Required = true };
        Option<Uri> readyUrl = new("--ready-url") { Description = "The labeler's readiness endpoint.", Required = true };
        Option<bool> backupConfirmed = new("--backup-confirmed") { Description = "Confirm that the database and external configuration have been backed up." };
        Option<bool> schemaCompatible = new("--schema-compatible") { Description = "Confirm that the target release supports the current schema and upgrade path." };
        Option<int> timeoutSeconds = new("--timeout-seconds")
        {
            Description = "The readiness timeout from 30 through 600 seconds.",
            DefaultValueFactory = _ => 180
        };
        Option<int> drainSeconds = new("--drain-seconds")
        {
            Description = "The graceful host-drain deadline from 30 through 600 seconds.",
            DefaultValueFactory = _ => 90
        };
        Option<bool> yes = new("--yes") { Description = "Confirm without an interactive prompt." };
        command.Options.Add(composeFile);
        command.Options.Add(service);
        command.Options.Add(image);
        command.Options.Add(previousImage);
        command.Options.Add(readyUrl);
        command.Options.Add(backupConfirmed);
        command.Options.Add(schemaCompatible);
        command.Options.Add(timeoutSeconds);
        command.Options.Add(drainSeconds);
        command.Options.Add(yes);
        command.SetAction((parseResult, cancellationToken) => ExecuteServiceUpdateAsync(
            parseResult.GetValue(composeFile),
            parseResult.GetValue(service),
            parseResult.GetValue(image),
            parseResult.GetValue(previousImage),
            parseResult.GetValue(readyUrl),
            parseResult.GetValue(backupConfirmed),
            parseResult.GetValue(schemaCompatible),
            parseResult.GetValue(timeoutSeconds),
            parseResult.GetValue(drainSeconds),
            parseResult.GetValue(yes),
            cancellationToken));
        return command;
    }

    /// <summary>Downloads, verifies, stages, and schedules one self-contained CLI update.</summary>
    private static async Task<int> ExecuteCliUpdateAsync(string? tag, bool confirmed, CancellationToken cancellationToken)
    {
        if (!IsStableReleaseTag(tag))
        {
            await Console.Error.WriteLineAsync("Supply an exact stable release tag in vMAJOR.MINOR.PATCH form.").ConfigureAwait(false);
            return 2;
        }

        if (!await ConfirmAsync(confirmed, $"Download and install the verified CLI release '{tag}'?", cancellationToken).ConfigureAwait(false))
        {
            return 2;
        }

        try
        {
            string runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
            if (runtimeIdentifier is not ("win-x64" or "linux-x64" or "linux-arm64"))
            {
                await Console.Error.WriteLineAsync($"Self-update is not supported for runtime '{runtimeIdentifier}'.").ConfigureAwait(false);
                return 2;
            }

            string executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("The running executable path is unavailable.");
            string executableName = Path.GetFileName(executablePath);
            string expectedExecutable = OperatingSystem.IsWindows()
                ? "idunno.Bluesky.Labeler.Cli.exe"
                : "idunno.Bluesky.Labeler.Cli";
            if (!string.Equals(executableName, expectedExecutable, StringComparison.OrdinalIgnoreCase))
            {
                await Console.Error.WriteLineAsync("Self-update requires the published self-contained CLI executable, not dotnet run or a renamed launcher.").ConfigureAwait(false);
                return 2;
            }

            string archiveName = $"idunno.Bluesky.Labeler.Cli-{runtimeIdentifier}.zip";
            byte[] archive = await DownloadVerifiedReleaseArchiveAsync(tag!, archiveName, cancellationToken).ConfigureAwait(false);
            string digest = Convert.ToHexStringLower(SHA256.HashData(archive));
            string installDirectory = Path.GetDirectoryName(executablePath)!;
            string updateId = Guid.NewGuid().ToString("N");
            string stagedPath = Path.Combine(installDirectory, $".{executableName}.{updateId}.new");
            string backupPath = Path.Combine(installDirectory, $".{executableName}.{updateId}.backup");
            await StageExecutableAsync(archive, executableName, stagedPath, cancellationToken).ConfigureAwait(false);
            await VerifyExecutableStartsAsync(stagedPath, cancellationToken).ConfigureAwait(false);
            StartReplacementHelper(executablePath, stagedPath, backupPath, updateId);
            await Console.Out.WriteLineAsync($"Verified SHA-256 {digest}. The update is staged and will be applied after this process exits; backup: {backupPath}").ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await Console.Error.WriteLineAsync("Update cancelled.").ConfigureAwait(false);
            return 130;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException or
                                           InvalidDataException or JsonException or InvalidOperationException or
                                           CryptographicException or Win32Exception or NotSupportedException or TimeoutException)
        {
            await Console.Error.WriteLineAsync($"CLI update failed ({exception.GetType().Name}).").ConfigureAwait(false);
            return 3;
        }
    }

    /// <summary>Downloads a release asset and verifies its GitHub artifact attestation before returning it.</summary>
    private static async Task<byte[]> DownloadVerifiedReleaseArchiveAsync(string tag, string assetName, CancellationToken cancellationToken)
    {
        using HttpClient client = new() { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("idunno.Bluesky.Labeler.Cli");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        Uri releaseUri = new($"https://api.github.com/repos/{GitHubRepository}/releases/tags/{Uri.EscapeDataString(tag)}");
        using JsonDocument release = await JsonDocument.ParseAsync(
            await client.GetStreamAsync(releaseUri, cancellationToken).ConfigureAwait(false),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        JsonElement root = release.RootElement;
        if (!root.TryGetProperty("tag_name", out JsonElement releaseTag) ||
            releaseTag.GetString() != tag ||
            root.TryGetProperty("draft", out JsonElement draft) && draft.GetBoolean() ||
            root.TryGetProperty("prerelease", out JsonElement prerelease) && prerelease.GetBoolean() ||
            !root.TryGetProperty("assets", out JsonElement assets) ||
            assets.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The requested tag is not a published stable release.");
        }

        Uri? assetUri = null;
        foreach (JsonElement asset in assets.EnumerateArray())
        {
            if (asset.TryGetProperty("name", out JsonElement name) &&
                name.GetString() == assetName &&
                asset.TryGetProperty("browser_download_url", out JsonElement downloadUrl) &&
                Uri.TryCreate(downloadUrl.GetString(), UriKind.Absolute, out Uri? parsedUri))
            {
                assetUri = parsedUri;
                break;
            }
        }

        if (assetUri is null ||
            assetUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(assetUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !assetUri.AbsolutePath.StartsWith($"/{GitHubRepository}/releases/download/{Uri.EscapeDataString(tag)}/", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The requested release asset is missing or has an unexpected download URL.");
        }

        using HttpResponseMessage assetResponse = await client.GetAsync(
            assetUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        assetResponse.EnsureSuccessStatusCode();
        if (assetResponse.Content.Headers.ContentLength is > MaximumReleaseArchiveSize)
        {
            throw new InvalidDataException("The release archive exceeds the 512 MiB update limit.");
        }

        byte[] archive = await ReadLimitedAsync(assetResponse.Content, MaximumReleaseArchiveSize, cancellationToken).ConfigureAwait(false);

        string temporaryPath = Path.Combine(Path.GetTempPath(), $"labeler-cli-{Guid.NewGuid():N}.zip");
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, archive, cancellationToken).ConfigureAwait(false);
            ProcessResult verification = await RunProcessAsync(
                executable: "gh",
                arguments: ["attestation", "verify", temporaryPath, "--repo", GitHubRepository],
                workingDirectory: Path.GetTempPath(),
                cancellationToken: cancellationToken,
                timeout: TimeSpan.FromMinutes(5)).ConfigureAwait(false);
            if (verification.ExitCode != 0)
            {
                throw new CryptographicException("GitHub CLI could not verify the release artifact attestation.");
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }

        return archive;
    }

    /// <summary>Extracts only the expected flat executable from a verified archive.</summary>
    private static async Task StageExecutableAsync(byte[] archive, string executableName, string stagedPath, CancellationToken cancellationToken)
    {
        using MemoryStream archiveStream = new(archive, writable: false);
        using ZipArchive zip = new(archiveStream, ZipArchiveMode.Read);
        ZipArchiveEntry? executable = zip.Entries.SingleOrDefault(entry =>
            string.Equals(entry.FullName, executableName, StringComparison.OrdinalIgnoreCase));
        if (executable is null || executable.Length is 0 or > 256 * 1024 * 1024)
        {
            throw new InvalidDataException("The verified archive does not contain the expected CLI executable.");
        }

        using Stream input = await executable.OpenAsync(cancellationToken).ConfigureAwait(false);
        using FileStream output = new(stagedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(stagedPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>Checks the staged executable can start before scheduling replacement.</summary>
    private static async Task VerifyExecutableStartsAsync(string executablePath, CancellationToken cancellationToken)
    {
        ProcessResult result = await RunProcessAsync(
            executable: executablePath,
            arguments: ["--help"],
            workingDirectory: Path.GetDirectoryName(executablePath)!,
            cancellationToken: cancellationToken,
            timeout: TimeSpan.FromSeconds(20)).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidDataException("The staged CLI did not pass its --help startup check.");
        }
    }

    /// <summary>Starts a platform helper that atomically replaces the executable after this process exits.</summary>
    private static void StartReplacementHelper(string executablePath, string stagedPath, string backupPath, string updateId)
    {
        int parentId = Environment.ProcessId;
        string scriptPath = Path.Combine(Path.GetTempPath(), $"labeler-cli-update-{updateId}.{(OperatingSystem.IsWindows() ? "ps1" : "sh")}");
        string script;
        ProcessStartInfo startInfo;
        if (OperatingSystem.IsWindows())
        {
            script = $$"""
                $ErrorActionPreference = 'Stop'
                $process = Get-Process -Id {{parentId}} -ErrorAction SilentlyContinue
                if ($process) { $process.WaitForExit() }
                [IO.File]::Replace('{{EscapePowerShell(stagedPath)}}', '{{EscapePowerShell(executablePath)}}', '{{EscapePowerShell(backupPath)}}', $true)
                try {
                  $check = Start-Process -FilePath '{{EscapePowerShell(executablePath)}}' -ArgumentList '--help' -NoNewWindow -PassThru -Wait
                  if ($check.ExitCode -ne 0) { throw 'The updated CLI failed its startup check.' }
                } catch {
                  $failed = '{{EscapePowerShell(executablePath)}}.failed'
                  if ([IO.File]::Exists('{{EscapePowerShell(executablePath)}}')) { [IO.File]::Move('{{EscapePowerShell(executablePath)}}', $failed, $true) }
                  [IO.File]::Move('{{EscapePowerShell(backupPath)}}', '{{EscapePowerShell(executablePath)}}', $true)
                  exit 1
                }
                """;
            startInfo = new ProcessStartInfo
            {
                FileName = ResolveExecutableOnPath("powershell.exe"),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
        }
        else
        {
            script = $$"""
                #!/bin/sh
                while kill -0 {{parentId}} 2>/dev/null; do sleep 0.2; done
                cp -- {{EscapeShell(executablePath)}} {{EscapeShell(backupPath)}} || exit 1
                if ! mv -- {{EscapeShell(stagedPath)}} {{EscapeShell(executablePath)}}; then
                  mv -- {{EscapeShell(backupPath)}} {{EscapeShell(executablePath)}}
                  exit 1
                fi
                if ! {{EscapeShell(executablePath)}} --help >/dev/null 2>&1; then
                  mv -- {{EscapeShell(backupPath)}} {{EscapeShell(executablePath)}}
                  exit 1
                fi
                """;
            startInfo = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(scriptPath);
        }

        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _ = Process.Start(startInfo) ?? throw new InvalidOperationException("The CLI replacement helper could not be started.");
    }

    /// <summary>Redeploys one Compose service by digest and restores its prior digest when readiness fails.</summary>
    private static async Task<int> ExecuteServiceUpdateAsync(
        FileInfo? composeFile,
        string? service,
        string? image,
        string? previousImage,
        Uri? readyUrl,
        bool backupConfirmed,
        bool schemaCompatible,
        int timeoutSeconds,
        int drainSeconds,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        if (composeFile is null ||
            !composeFile.Exists ||
            string.IsNullOrWhiteSpace(service) ||
            !Regex.IsMatch(service, "^[A-Za-z0-9][A-Za-z0-9_-]{0,62}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) ||
            !IsLabelerImageDigest(image) ||
            !IsLabelerImageDigest(previousImage) ||
            image == previousImage ||
            readyUrl is null ||
            !IsReadinessUri(readyUrl) ||
            !backupConfirmed ||
            !schemaCompatible ||
            timeoutSeconds is < 30 or > 600 ||
            drainSeconds is < 30 or > 600)
        {
            await Console.Error.WriteLineAsync("Supply an existing Compose file, service, distinct pinned GHCR digests, readiness URL, backup/schema confirmations, and timeout from 30 through 600 seconds.").ConfigureAwait(false);
            return 2;
        }

        if (!await ConfirmAsync(
            confirmed,
            $"Redeploy Compose service '{service}' from '{previousImage}' to '{image}'? The previous digest will be restored if readiness fails.",
            cancellationToken).ConfigureAwait(false))
        {
            return 2;
        }

        string projectDirectory = composeFile.DirectoryName!;
        string overridePath = Path.Combine(projectDirectory, $".labeler-update-{Guid.NewGuid():N}.yaml");
        try
        {
            await WriteComposeOverrideAsync(overridePath, service, image!, cancellationToken).ConfigureAwait(false);
            ProcessResult services = await RunComposeAsync(composeFile, overridePath, projectDirectory, image!, ["config", "--services"], cancellationToken).ConfigureAwait(false);
            if (services.ExitCode != 0 ||
                !services.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Contains(service, StringComparer.Ordinal))
            {
                await Console.Error.WriteLineAsync("The selected service is not present in the Compose project.").ConfigureAwait(false);
                return 2;
            }

            ProcessResult targetPull = await RunComposeAsync(composeFile, overridePath, projectDirectory, image!, ["pull", service], cancellationToken).ConfigureAwait(false);
            if (targetPull.ExitCode != 0)
            {
                await Console.Error.WriteLineAsync("The target image could not be pulled; the running service was not changed.").ConfigureAwait(false);
                return 5;
            }

            await WriteComposeOverrideAsync(overridePath, service, previousImage!, cancellationToken).ConfigureAwait(false);
            ProcessResult rollbackPull = await RunComposeAsync(composeFile, overridePath, projectDirectory, previousImage!, ["pull", service], cancellationToken).ConfigureAwait(false);
            if (rollbackPull.ExitCode != 0)
            {
                await Console.Error.WriteLineAsync("The previous image could not be pulled; deployment was not changed because rollback could not be guaranteed.").ConfigureAwait(false);
                return 5;
            }

            await WriteComposeOverrideAsync(overridePath, service, image!, cancellationToken).ConfigureAwait(false);
            ProcessResult stop = await RunComposeAsync(
                composeFile,
                overridePath,
                projectDirectory,
                image!,
                ["stop", "--timeout", drainSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture), service],
                cancellationToken).ConfigureAwait(false);
            if (stop.ExitCode != 0)
            {
                await Console.Error.WriteLineAsync("Compose could not gracefully stop the service; the update was not started.").ConfigureAwait(false);
                return 5;
            }

            ProcessResult update = await RunComposeAsync(
                composeFile,
                overridePath,
                projectDirectory,
                image!,
                ["up", "-d", "--no-deps", "--force-recreate", service],
                cancellationToken).ConfigureAwait(false);
            if (update.ExitCode != 0)
            {
                await Console.Error.WriteLineAsync("Compose failed to redeploy the service; attempting to restore the previous image.").ConfigureAwait(false);
                bool composeRollbackSucceeded = await RollbackComposeAsync(
                    composeFile,
                    overridePath,
                    projectDirectory,
                    service,
                    previousImage!,
                    readyUrl,
                    timeoutSeconds,
                    cancellationToken).ConfigureAwait(false);
                await Console.Error.WriteLineAsync(composeRollbackSucceeded
                    ? $"The previous image '{previousImage}' was restored and passed its readiness check."
                    : "Rollback did not pass readiness; inspect the Compose service and preserve its external data before further action.").ConfigureAwait(false);
                return 5;
            }

            if (await WaitForReadinessAsync(readyUrl, TimeSpan.FromSeconds(timeoutSeconds), cancellationToken).ConfigureAwait(false))
            {
                await Console.Out.WriteLineAsync($"Compose service '{service}' is ready on {image}.").ConfigureAwait(false);
                return 0;
            }

            await Console.Error.WriteLineAsync("The updated service did not become ready; restoring the previous image.").ConfigureAwait(false);
            bool rollbackSucceeded = await RollbackComposeAsync(
                composeFile,
                overridePath,
                projectDirectory,
                service,
                previousImage!,
                readyUrl,
                timeoutSeconds,
                cancellationToken).ConfigureAwait(false);
            await Console.Error.WriteLineAsync(rollbackSucceeded
                ? $"The previous image '{previousImage}' was restored and passed its readiness check."
                : "Rollback did not pass readiness; inspect the Compose service and preserve its external data before further action.").ConfigureAwait(false);
            return 5;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await Console.Error.WriteLineAsync("Service update cancelled. The deployment may have changed; inspect Compose status and the readiness endpoint before retrying.").ConfigureAwait(false);
            return 130;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception or HttpRequestException or InvalidOperationException or TimeoutException)
        {
            await Console.Error.WriteLineAsync($"Service update failed ({exception.GetType().Name}).").ConfigureAwait(false);
            return 3;
        }
        finally
        {
            File.Delete(overridePath);
        }
    }

    /// <summary>Restores the previous Compose digest and verifies the restored service readiness.</summary>
    private static async Task<bool> RollbackComposeAsync(
        FileInfo composeFile,
        string overridePath,
        string projectDirectory,
        string service,
        string previousImage,
        Uri readyUrl,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        await WriteComposeOverrideAsync(overridePath, service, previousImage, cancellationToken).ConfigureAwait(false);
        ProcessResult rollback = await RunComposeAsync(
            composeFile,
            overridePath,
            projectDirectory,
            previousImage,
            ["up", "-d", "--no-deps", "--force-recreate", service],
            cancellationToken).ConfigureAwait(false);
        return rollback.ExitCode == 0 &&
            await WaitForReadinessAsync(readyUrl, TimeSpan.FromSeconds(timeoutSeconds), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes a temporary Compose override without modifying the operator's project files.</summary>
    private static Task WriteComposeOverrideAsync(string path, string service, string image, CancellationToken cancellationToken)
    {
        string contents = $"services:{Environment.NewLine}  {service}:{Environment.NewLine}    image: {image}{Environment.NewLine}";
        return File.WriteAllTextAsync(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
    }

    /// <summary>Reads an HTTP response while enforcing the configured archive size limit.</summary>
    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, int maximumBytes, CancellationToken cancellationToken)
    {
        using Stream input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using MemoryStream output = new();
        byte[] buffer = new byte[64 * 1024];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (output.Length + read > maximumBytes)
            {
                throw new InvalidDataException("The release archive exceeds the 512 MiB update limit.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        if (output.Length == 0)
        {
            throw new InvalidDataException("The release archive is empty.");
        }

        return output.ToArray();
    }

    /// <summary>Runs Docker Compose with the base project and a temporary image-only override.</summary>
    private static Task<ProcessResult> RunComposeAsync(
        FileInfo composeFile,
        string overridePath,
        string projectDirectory,
        string image,
        IReadOnlyList<string> composeArguments,
        CancellationToken cancellationToken)
    {
        List<string> arguments = ["compose", "--project-directory", projectDirectory, "--file", composeFile.FullName, "--file", overridePath];
        arguments.AddRange(composeArguments);
        ProcessStartInfo startInfo = new(ResolveExecutableOnPath("docker"))
        {
            WorkingDirectory = projectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["LABELER_IMAGE"] = image;
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return RunProcessAsync(startInfo, TimeSpan.FromMinutes(5), cancellationToken);
    }

    /// <summary>Checks an unauthenticated readiness endpoint for a bounded interval without following redirects.</summary>
    private static async Task<bool> WaitForReadinessAsync(Uri readyUrl, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using SocketsHttpHandler handler = new() { AllowAutoRedirect = false };
        using HttpClient client = new(handler) { Timeout = TimeSpan.FromSeconds(5) };
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using HttpResponseMessage response = await client.GetAsync(readyUrl, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch (HttpRequestException)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
                continue;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
                continue;
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>Runs a process with argument-list escaping and captures its output.</summary>
    private static async Task<ProcessResult> RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(ResolveExecutableOnPath(executable))
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return await RunProcessAsync(startInfo, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs a configured process with a finite deadline and captures its output.</summary>
    private static async Task<ProcessResult> RunProcessAsync(
        ProcessStartInfo startInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        string executable = startInfo.FileName;
        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start '{executable}'.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using CancellationTokenSource timeoutSource = new(timeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException($"Process '{executable}' exceeded its {timeout} timeout.");
        }

        return new(process.ExitCode, await outputTask.ConfigureAwait(false), await errorTask.ConfigureAwait(false));
    }

    /// <summary>Accepts only a stable semantic release tag.</summary>
    internal static bool IsStableReleaseTag(string? tag) =>
        tag is not null &&
        Regex.IsMatch(
            tag,
            "^v[0-9]+\\.[0-9]+\\.[0-9]+$",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
            TimeSpan.FromSeconds(1));

    /// <summary>Accepts only the published labeler image repository pinned by a SHA-256 digest.</summary>
    internal static bool IsLabelerImageDigest(string? image) =>
        image is not null &&
        Regex.IsMatch(
            image,
            $"^{Regex.Escape(LabelerImageRepository)}@sha256:[0-9a-f]{{64}}$",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
            TimeSpan.FromSeconds(1));

    /// <summary>Accepts only HTTP(S) readiness endpoints without credentials, query, or fragment.</summary>
    internal static bool IsReadinessUri(Uri uri) =>
        uri.IsAbsoluteUri &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment) &&
        uri.AbsolutePath == "/health/labeler/ready";

    /// <summary>Escapes a value for a single-quoted PowerShell literal.</summary>
    private static string EscapePowerShell(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    /// <summary>Escapes a value for a single-quoted POSIX shell literal.</summary>
    private static string EscapeShell(string value) => $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";

    /// <summary>Finds a required executable on PATH and returns its absolute path.</summary>
    private static string ResolveExecutableOnPath(string executable)
    {
        if (Path.IsPathFullyQualified(executable))
        {
            return executable;
        }

        string[] extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [""];
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string extension in extensions)
            {
                string candidate = Path.Combine(directory, executable + (Path.HasExtension(executable) ? "" : extension));
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        throw new FileNotFoundException($"Required executable '{executable}' was not found on PATH.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
