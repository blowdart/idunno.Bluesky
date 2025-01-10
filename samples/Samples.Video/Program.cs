// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Reflection;

using Microsoft.Extensions.Logging;

using idunno.Bluesky;
using idunno.Bluesky.Embed;

using Samples.Common;

namespace Samples.Video
{
    public sealed class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Necessary to render emojis.
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var parser = Helpers.ConfigureCommandLine(PerformOperations);
            await parser.InvokeAsync(args);

            return 0;
        }

        static async Task PerformOperations(string? handle, string? password, string? authCode, Uri? proxyUri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(handle);
            ArgumentNullException.ThrowIfNullOrEmpty(password);

            // Uncomment the next line to route all requests through Fiddler Everywhere
            // proxyUri = new Uri("http://localhost:8866");

            // Uncomment the next line to route all requests  through Fiddler Classic
            // proxyUri = new Uri("http://localhost:8888");

            // If a proxy is being used turn off certificate revocation checks.
            //
            // WARNING: this setting can introduce security vulnerabilities.
            // The assumption in these samples is that any proxy is a debugging proxy,
            // which tend to not support CRLs in the proxy HTTPS certificates they generate.
            bool checkCertificateRevocationList = true;
            if (proxyUri is not null)
            {
                checkCertificateRevocationList = false;
            }

            // Change the log level in the ConfigureConsoleLogging() to enable logging
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Debug))

            // Create a new BlueSkyAgent
            using (var agent = new BlueskyAgent(proxyUri: proxyUri, checkCertificateRevocationList: checkCertificateRevocationList, loggerFactory: loggerFactory))
            {
                // Test code goes here.

                // Delete if your test code does not require authentication
                // START-AUTHENTICATION
                var loginResult = await agent.Login(handle, password, authCode, cancellationToken: cancellationToken);
                if (!loginResult.Succeeded)
                {
                    if (loginResult.AtErrorDetail is not null &&
                        string.Equals(loginResult.AtErrorDetail.Error!, "AuthFactorTokenRequired", StringComparison.OrdinalIgnoreCase))
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Login requires an authentication code.");
                        Console.WriteLine("Check your email and use --authCode to specify the authentication code.");
                        Console.ForegroundColor = oldColor;

                        return;
                    }
                    else
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Login failed.");
                        Console.ForegroundColor = oldColor;

                        if (loginResult.AtErrorDetail is not null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;

                            Console.WriteLine($"Server returned {loginResult.AtErrorDetail.Error} / {loginResult.AtErrorDetail.Message}");
                            Console.ForegroundColor = oldColor;

                            return;
                        }
                    }
                }
                // END-AUTHENTICATION

                {
                    byte[] videoAsBytes;
                    using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Samples.Video.Flowers.mp4")!)
                    using (MemoryStream memoryStream = new())
                    {
                        resourceStream.CopyTo(memoryStream);
                        videoAsBytes = memoryStream.ToArray();
                    }

                    var videoUploadLimitsResult = await agent.GetVideoUploadLimits(cancellationToken: cancellationToken);
                    videoUploadLimitsResult.EnsureSucceeded();

                    if (!videoUploadLimitsResult.Result.CanUpload ||
                        videoUploadLimitsResult.Result.RemainingDailyVideos == 0 ||
                        videoUploadLimitsResult.Result.RemainingDailyBytes < (ulong)videoAsBytes.LongLength)
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Account has hit its video limits");
                        Console.WriteLine("Can upload: {videoUploadLimitsResult.Result.CanUpload}");
                        Console.WriteLine("Daily videos remaining: {videoUploadLimitsResult.Result.RemainingDailyVideos}");
                        Console.WriteLine("Daily bytes remaining: {videoUploadLimitsResult.Result.RemainingBytesVideos}");
                        Console.ForegroundColor = oldColor;
                        return;
                    }

                    // We're using a random file name here, because if a video upload with the same name already exists the upload fails.
                    var videoUploadResult = await agent.UploadVideo(
                        Path.GetRandomFileName() + ".mp4",
                        videoAsBytes,
                        cancellationToken: cancellationToken);

                    // Quick fail - you'd want to be more graceful in handling errors.
                    videoUploadResult.EnsureSucceeded();

                    while (videoUploadResult.Result.State == idunno.Bluesky.Video.JobState.InProgress &&
                        videoUploadResult.Succeeded &&
                        !cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"Video job # {videoUploadResult.Result.JobId} processing, progress {videoUploadResult.Result.Progress}");
                        await Task.Delay(1000, cancellationToken: cancellationToken);
                        videoUploadResult = await agent.GetVideoJobStatus(videoUploadResult.Result.JobId, cancellationToken: cancellationToken);
                        videoUploadResult.EnsureSucceeded();
                    }

                    if (!videoUploadResult.Succeeded ||
                        videoUploadResult.Result.Blob is null ||
                        videoUploadResult.Result.State != idunno.Bluesky.Video.JobState.Completed)
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Video job # {videoUploadResult.Result.JobId} failed. {videoUploadResult.Result.Error} {videoUploadResult.Result.Message}.");
                        Console.ForegroundColor = oldColor;
                        return;
                    }

                    Console.WriteLine($"Video job # {videoUploadResult.Result.JobId} finished processing, {videoUploadResult.Result.State}");

                    byte[] captionsAsBytes;
                    using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Samples.Video.captions.vtt")!)
                    using (MemoryStream memoryStream = new())
                    {
                        resourceStream.CopyTo(memoryStream);
                        captionsAsBytes = memoryStream.ToArray();
                    }

                    var captionUploadResult = await agent.UploadCaptions(captionsAsBytes, "en", cancellationToken: cancellationToken).ConfigureAwait(false);
                    if (!captionUploadResult.Succeeded)
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Caption upload failed.{captionUploadResult.StatusCode} {captionUploadResult.AtErrorDetail?.Error} {captionUploadResult.AtErrorDetail?.Message}.");
                        Console.ForegroundColor = oldColor;
                        return;
                    }

                    EmbeddedVideo video = new(videoUploadResult.Result.Blob, altText: "Alt Text", captions: captionUploadResult.Result);

                    var postResult = await agent.Post("With video and captions", video: video, cancellationToken: cancellationToken);
                    postResult.EnsureSucceeded();
                    Debugger.Break();

                    await agent.DeletePost(postResult.Result.StrongReference, cancellationToken: cancellationToken);
                }

                {
                    byte[] videoAsBytes;
                    using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Samples.Video.DroneBeach.mp4")!)
                    using (MemoryStream memoryStream = new())
                    {
                        resourceStream.CopyTo(memoryStream);
                        videoAsBytes = memoryStream.ToArray();
                    }

                    var videoUploadResult = await agent.UploadVideo(
                        Path.GetRandomFileName() + ".mp4",
                        videoAsBytes,
                        cancellationToken: cancellationToken);

                    videoUploadResult.EnsureSucceeded();

                    while (videoUploadResult.Result.State == idunno.Bluesky.Video.JobState.InProgress &&
                        videoUploadResult.Succeeded &&
                        !cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"Video job # {videoUploadResult.Result.JobId} processing, progress {videoUploadResult.Result.Progress}");
                        await Task.Delay(1000, cancellationToken: cancellationToken);
                        videoUploadResult = await agent.GetVideoJobStatus(videoUploadResult.Result.JobId, cancellationToken: cancellationToken);
                        videoUploadResult.EnsureSucceeded();
                    }

                    if (!videoUploadResult.Succeeded ||
                        videoUploadResult.Result.Blob is null ||
                        videoUploadResult.Result.State != idunno.Bluesky.Video.JobState.Completed)
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Video job # {videoUploadResult.Result.JobId} failed. {videoUploadResult.Result.Error} {videoUploadResult.Result.Message}.");
                        Console.ForegroundColor = oldColor;
                        return;
                    }

                    Console.WriteLine($"Video job # {videoUploadResult.Result.JobId} finished processing, {videoUploadResult.Result.State}");

                    EmbeddedVideo video = new(videoUploadResult.Result.Blob, altText: "Alt Text");

                    PostBuilder postBuilder = new("PostBuilder with video test");
                    postBuilder.Add(video);

                    var postResult = await agent.Post(postBuilder, cancellationToken: cancellationToken);
                    postResult.EnsureSucceeded();
                    Debugger.Break();

                    await agent.DeletePost(postResult.Result.StrongReference, cancellationToken: cancellationToken);
                }
            }
            return;
        }
    }
}
