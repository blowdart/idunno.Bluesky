// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;

using idunno.AtProto;
using idunno.Bluesky;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Video;

using Microsoft.Extensions.Logging;

using Samples.Common;

namespace Samples.PartialUploads;

public sealed class Program
{
    static async Task<int> Main(string[] args)
    {
        // Necessary to render emojis.
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var parser = Helpers.ConfigureCommandLine(
            args,
            "BlueskyAgent Partial Uploads Sample",
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
        // TODO put back to debug
        using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Error))

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

            // Get information about the file to upload.
            string filePath = "sample.mp4";
            int fileSize = (int)new FileInfo(filePath).Length;

            // Check the authenticated user has the ability to upload a video of this size.
            var getVideoUploadLimitsResult = await agent.GetUploadLimits(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!getVideoUploadLimitsResult.Succeeded)
            {
                Console.WriteLine($"❌ Failed to get video upload limits.{Environment.NewLine}    Server returned {getVideoUploadLimitsResult.StatusCode} / {getVideoUploadLimitsResult.AtErrorDetail?.Error} / {getVideoUploadLimitsResult.AtErrorDetail?.Message}");
                return;
            }

            if (getVideoUploadLimitsResult.Result.RemainingDailyVideos == 0)
            {
                Console.WriteLine($"❌ No remaining daily video uploads.");
                return;
            }

            if (getVideoUploadLimitsResult.Result.RemainingDailyBytes < fileSize)
            {
                Console.WriteLine($"❌ Video file is too large to upload. Max size is {getVideoUploadLimitsResult.Result.RemainingDailyBytes} bytes, but the video file is {fileSize} bytes.");
                return;
            }

            // Start the multipart upload process.
            // This will return a jobId, the number of parts to upload, and the size for each part.
            var startUploadResult = await agent.StartUpload(
                size: fileSize,
                mimeType: "video/mp4",
                name: Path.GetFileName(filePath),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            startUploadResult.EnsureSucceeded();

            Console.WriteLine($"🚀 Starting upload for jobID {startUploadResult.Result.JobId} with {startUploadResult.Result.PartCount} partial uploads.");

            var uploadPartResponses = new AtProtoHttpResult<UploadPartResponse>?[startUploadResult.Result.PartCount];
            var pool = ArrayPool<byte>.Shared;

            // https://alex-bsky.leaflet.pub/3mthoelgvrs2h suggests "a concurrency of 3 [as] ideal in most environments for parallelizing video uploads."
            ParallelOptions parallelOptions = new()
            {
                MaxDegreeOfParallelism = 3,
                CancellationToken = cancellationToken
            };

            await Parallel.ForAsync(0, startUploadResult.Result.PartCount, parallelOptions, async (uploadPart, ct) =>
            {
                string jobId = startUploadResult.Result.JobId;
                int partNumber = uploadPart + 1; // Part numbers are 1-based, not 0-based.
                int offset = uploadPart * startUploadResult.Result.PartSize;
                int partSize = uploadPart == startUploadResult.Result.PartCount - 1
                    ? fileSize - offset
                    : startUploadResult.Result.PartSize;
                byte[] partBytes = pool.Rent(startUploadResult.Result.PartSize);

                try
                {
                    using (var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        sourceStream.Position = offset;

                        Console.WriteLine($"📃 Reading {partSize} bytes from offset {offset} for part {partNumber}.");
                        await sourceStream.ReadAsync(partBytes.AsMemory(0, partSize), ct).ConfigureAwait(false);

                        Console.WriteLine($"💾 Uploading part {partNumber} for jobID {jobId} with size {partBytes.Length} bytes.");
                        uploadPartResponses[uploadPart] = await agent.UploadPart(
                            jobId: jobId,
                            part: partNumber,
                            bytes: partBytes[0..partSize],
                            timeout: TimeSpan.FromMinutes(60),
                            cancellationToken: ct).ConfigureAwait(false);
                    }

                    Console.WriteLine($"✅ Finished uploading part {partNumber} for jobID {jobId}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Exception occurred while uploading part {partNumber} for jobID {jobId}.{Environment.NewLine}    Exception: {ex}");
                }
                finally
                {
                    pool.Return(partBytes);
                }
            }).ConfigureAwait(false);

            // Check if any part upload failed. If any part upload failed, abort the upload and exit the sample.
            foreach (var uploadPartResult in uploadPartResponses)
            {
                if (uploadPartResult is null || !uploadPartResult.Succeeded)
                {
                    // If any part upload failed, abort the upload, to free the reserved resources from our upload allowance, and then exit the sample.
                    // You could also retry an individual part upload if you wanted to.

                    var abortUploadResult = await agent.AbortUpload(
                        jobId: startUploadResult.Result.JobId,
                        cancellationToken: cancellationToken);

                    if (abortUploadResult.Succeeded)
                    {
                        Console.WriteLine($"❌ Part upload failed for jobID {startUploadResult.Result.JobId}, job aborted successfully.");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Part upload failed for jobID {startUploadResult.Result.JobId}, job abort failed.{Environment.NewLine}    Server returned {abortUploadResult.StatusCode} / {abortUploadResult.AtErrorDetail?.Error} / {abortUploadResult.AtErrorDetail?.Message}");
                    }
                    return;
                }
            }

            // If all parts uploaded successfully, we will now finish the upload, which then starts it processing.
            var finishUploadResult = await agent.FinishUpload(
                jobId: startUploadResult.Result.JobId,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // If the finish upload call failed, or if the job status is failed or unknown, abort the upload and exit the sample.
            if (!finishUploadResult.Succeeded ||
                finishUploadResult.Result.JobStatus is not null &&
                (finishUploadResult.Result.JobStatus!.State == JobState.Failed ||
                finishUploadResult.Result.JobStatus!.State == JobState.Unknown))
            {
                var abortUploadResult = await agent.AbortUpload(
                    jobId: startUploadResult.Result.JobId,
                    cancellationToken: cancellationToken);

                if (abortUploadResult.Succeeded)
                {
                    Console.WriteLine($"🗑️ FinishUpload failed for jobID {startUploadResult.Result.JobId}, job aborted successfully.");
                }
                else
                {
                    Console.WriteLine($"❌ FinishUpload failed for jobID {startUploadResult.Result.JobId}, job abort failed.{Environment.NewLine}    Server returned {abortUploadResult.StatusCode} / {abortUploadResult.AtErrorDetail?.Error} / {abortUploadResult.AtErrorDetail?.Message}");
                }
                return;
            }

            Console.WriteLine($"✅ Finished upload for jobID {startUploadResult.Result.JobId}");

            // Poll the job status until it is completed, failed, or unknown.
            // This is a long running operation and may take several minutes to complete, depending on the size of the video and the current load on the server.
            AtProtoHttpResult<JobStatus> getJobStatusResult;
            bool finished = false;

            // Polling interval of 1 second is an unreasonably high frequency for a production application,
            // but is useful for a sample to demonstrate the job status changing from processing to completed.
            TimeSpan pollingInterval = new(0, 0, 1);
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
                        case JobState.Unknown: // This should never happen, but if it does, we will treat it as a failure.
                            finished = true;
                            break;
                        default:
                            break;
                    }

                    if (!finished)
                    {
                        Console.WriteLine("⌛ Waiting for job to complete. Current state: " + getJobStatusResult.Result.State);
                        Thread.Sleep(pollingInterval);
                    }
                }
            } while (getJobStatusResult.Succeeded && !finished);

            if (getJobStatusResult.Succeeded)
            {
                Console.WriteLine($"✅ Job completed with state: {getJobStatusResult.Result.State}");

                Post post = new("Test parallel multipart video upload");
                post.Embed(new EmbeddedVideo(getJobStatusResult.Result.Blob!, altText: "Alt Text"));
                await agent.Post(post, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                Console.WriteLine($"❌ Failed to get job status for jobID {finishUploadResult.Result.CompletedJobId}.{Environment.NewLine}    Server returned {getJobStatusResult.StatusCode} / {getJobStatusResult.AtErrorDetail?.Error} / {getJobStatusResult.AtErrorDetail?.Message}");
            }
        }
    }
}