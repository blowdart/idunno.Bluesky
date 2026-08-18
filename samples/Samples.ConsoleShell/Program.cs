// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;

using idunno.AtProto;
using idunno.Bluesky;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Video;

using Microsoft.Extensions.Logging;

using Samples.Common;

namespace Samples.ConsoleShell;

public sealed class Program
{
    static async Task<int> Main(string[] args)
    {
        // Necessary to render emojis.
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var parser = Helpers.ConfigureCommandLine(
            args,
            "BlueskyAgent Console Demonstration Template",
            PerformOperations);

        return await parser.InvokeAsync();
    }

    static async Task PerformOperations(string? userHandle, string? password, string? authCode, Uri? proxyUri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userHandle);
        ArgumentException.ThrowIfNullOrEmpty(password);

        // Uncomment the next line to route all requests through Fiddler Everywhere
        proxyUri = new Uri("http://localhost:8866");

        // Uncomment the next line to route all requests  through Fiddler Classic
        // proxyUri = new Uri("http://localhost:8888");

        // Change the log level in the ConfigureConsoleLogging() to enable logging
        using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Debug))

        // Create a new BlueSkyAgent
        using (var agent = new BlueskyAgent(
            options: new BlueskyAgentOptions()
            {
                LoggerFactory = loggerFactory,

                HttpClientOptions = new HttpClientOptions()
                {
                    ProxyUri = proxyUri
                },
            }))
        {
            // Delete if your test code does not require authentication
            // START-AUTHENTICATION
            var loginResult = await agent.Login(userHandle, password, authCode, cancellationToken: cancellationToken);
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
          
            byte[] video = await File.ReadAllBytesAsync("C:\\Users\\BarryDorrans\\Downloads\\sample-5s.mp4", cancellationToken);

            var startUploadResult = await agent.StartUpload(
                size: video.Length,
                mimeType: "video/mp4",
                name: "sample-5s.mp4",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            startUploadResult.EnsureSucceeded();
            Console.WriteLine($"Started upload for jobID {startUploadResult.Result.JobId} with {startUploadResult.Result.PartCount} parts.");
            Console.WriteLine(agent.Credentials!.AccessJwt);

            bool partUploadFailed = false;

            try
            {
                var uploadPartTasks = new Task[startUploadResult.Result.PartCount];
                AtProtoHttpResult<UploadPartResponse>[] uploadPartResults = new AtProtoHttpResult<UploadPartResponse>[startUploadResult.Result.PartCount];

                for (int i = 0; i < startUploadResult.Result.PartCount; i++)
                {
                    byte[] part;
                    int offset = (i * startUploadResult.Result.PartSize);

                    if (i != startUploadResult.Result.PartCount - 1)
                    {
                        part = video[offset..(offset + startUploadResult.Result.PartSize)];
                    }
                    else
                    {
                        part = video[offset..];
                    }

                    //uploadPartTasks[i] = Task.Run(async () =>
                    //{
                    //    uploadPartResults[i] = await agent.UploadPart(
                    //        jobId: startUploadResult.Result.JobId,
                    //        part: i + 1,
                    //        bytes: part,
                    //        timeout: TimeSpan.FromMinutes(60),
                    //        cancellationToken: cancellationToken).ConfigureAwait(false);
                    //}, cancellationToken: cancellationToken);

                    uploadPartResults[i] = await agent.UploadPart(
                        jobId: startUploadResult.Result.JobId,
                        part: i + 1,
                        bytes: part,
                        timeout: TimeSpan.FromMinutes(60),
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (!uploadPartResults[i].Succeeded)
                    {
                        var abortUploadResult = await agent.AbortUpload(
                            jobId: startUploadResult.Result.JobId,
                            cancellationToken: cancellationToken);
                        partUploadFailed = true;
                        abortUploadResult.EnsureSucceeded();
                        break;
                    }
                }

                //await Task.WhenAll(uploadPartTasks).ConfigureAwait(false);
            }
            catch
            {
                partUploadFailed = true;
                var abortUploadResult = await agent.AbortUpload(
                    jobId: startUploadResult.Result.JobId,
                    cancellationToken: cancellationToken);
                abortUploadResult.EnsureSucceeded();
            }

            if (partUploadFailed)
            {
                Console.WriteLine($"Part upload failed for jobID {startUploadResult.Result.JobId}, job aborted.");
                return;
            }

            var finishUploadResult = await agent.FinishUpload(
                jobId: startUploadResult.Result.JobId,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            finishUploadResult.EnsureSucceeded();

            AtProtoHttpResult<JobStatus> getJobStatusResult;
            bool finished = false;
            do
            {
                getJobStatusResult = await agent.GetJobStatus(
                    jobId: finishUploadResult.Result.CompletedJobId,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (getJobStatusResult.Succeeded)
                {
                    switch (getJobStatusResult.Result.State)
                    {
                        case JobState.Completed:
                            finished = true;
                            break;
                        case JobState.Failed:
                            finished = true;
                            break;
                        case JobState.Unknown:
                            finished = true;
                            break;
                        default:
                            break;
                    }
                }
            } while (getJobStatusResult.Succeeded && !finished);

            getJobStatusResult.EnsureSucceeded();

            if (getJobStatusResult.Result.State != JobState.Completed)
            {
                var abortUploadResult = await agent.AbortUpload(
                    jobId: startUploadResult.Result.JobId,
                    cancellationToken: cancellationToken);
                abortUploadResult.EnsureSucceeded();
            }

            Post post = new("Test multipart video upload");
            post.Embed(new EmbeddedVideo(getJobStatusResult.Result.Blob!, altText: "Alt Text"));
        }
    }
}