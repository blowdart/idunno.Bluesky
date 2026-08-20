// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

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

            // Read a sample video file. You can change this to any video file you want to upload.
            string filePath = "mp4-99mb-sample.mp4";

            // Read the entire video file into memory. This is not recommended for large files, but is done here for simplicity.
            byte[] video = await File.ReadAllBytesAsync(filePath, cancellationToken);

            // Start the multipart upload process.
            // This will return a jobId, the number of parts to upload, and the size for each part.
            var startUploadResult = await agent.StartUpload(
                size: video.Length,
                mimeType: "video/mp4",
                name: Path.GetFileName(filePath),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            startUploadResult.EnsureSucceeded();

            Console.WriteLine($"Started upload for jobID {startUploadResult.Result.JobId} requiring {startUploadResult.Result.PartCount} partial uploads.");

            // Quick and dirty parallel upload of the parts. You may want to use Parallel.ForEach or some other method to control the degree of parallelism, but this is a simple example.
            var uploadPartTasks = new Task[startUploadResult.Result.PartCount];
            var uploadPartResponses = new AtProtoHttpResult<UploadPartResponse>?[startUploadResult.Result.PartCount];

            // Local function to upload a part. This is defined here so it can access the uploadPartResponses array.
            async Task UploadPart(string jobId, int partNumber, byte[] bytes, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Uploading part {partNumber} for jobID {jobId} with size {bytes.Length} bytes.");

                try
                {
                    AtProtoHttpResult<UploadPartResponse> uploadPartResult = await agent.UploadPart(
                    jobId: jobId,
                    part: partNumber,
                    bytes: bytes,
                    timeout: TimeSpan.FromMinutes(60),
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (!uploadPartResult.Succeeded)
                    {
                        Console.WriteLine($"** Failed to upload part {partNumber} for jobID {jobId}.{Environment.NewLine}    Server returned {uploadPartResult.StatusCode} / {uploadPartResult.AtErrorDetail?.Error} / {uploadPartResult.AtErrorDetail?.Message}");
                    }

                    uploadPartResponses[partNumber - 1] = uploadPartResult;

                }
                catch (Exception ex)
                {
                    // If any exception occurs during the part upload ensure the results are null
                    Console.WriteLine($"** Exception occurred while uploading part {partNumber} for jobID {jobId}.{Environment.NewLine}    Exception: {ex}");
                }
            }

            for (int i = 0; i < startUploadResult.Result.PartCount; i++)
            {
                // Capture the variables for UploadPart to avoid closure issues in the loop.
                string jobId = startUploadResult.Result.JobId;
                int partNumber = i + 1; // Part numbers are 1-based, not 0-based.
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
                uploadPartTasks[i] = Task.Run(async () => await UploadPart(jobId, partNumber, part, cancellationToken), cancellationToken);
            }

            // Wait for all part uploads to complete
            await Task.WhenAll(uploadPartTasks).ConfigureAwait(false);

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

                    Console.WriteLine($"Part upload failed for jobID {startUploadResult.Result.JobId}, job aborted.");
                    return;
                }
            }

            // If all parts uploaded successfully, we will now finish the upload, which then starts it processing.
            var finishUploadResult = await agent.FinishUpload(
                jobId: startUploadResult.Result.JobId,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            finishUploadResult.EnsureSucceeded();

            // If the finish upload call failed, or if the job status is failed or unknown, abort the upload and exit the sample.
            if (!finishUploadResult.Succeeded ||
                finishUploadResult.Result.JobStatus is not null &&
                (finishUploadResult.Result.JobStatus!.State == JobState.Failed ||
                finishUploadResult.Result.JobStatus!.State == JobState.Unknown))
            {
                var abortUploadResult = await agent.AbortUpload(
                    jobId: startUploadResult.Result.JobId,
                    cancellationToken: cancellationToken);

                Console.WriteLine($"FinishUpload failed for jobID {startUploadResult.Result.JobId}, job aborted.");
                return;
            }

            // Poll the job status until it is completed, failed, or unknown.
            // This is a long running operation and may take several minutes to complete, depending on the size of the video and the current load on the server.
            AtProtoHttpResult<JobStatus> getJobStatusResult;
            bool finished = false;
            TimeSpan pollingInterval = new(0, 0, 15);
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

                    Console.WriteLine("Waiting for job to complete. Current state: " + getJobStatusResult.Result.State);
                    Thread.Sleep(pollingInterval);
                }
            } while (getJobStatusResult.Succeeded && !finished);

            getJobStatusResult.EnsureSucceeded();

            Post post = new("Test parallel multipart video upload");
            post.Embed(new EmbeddedVideo(getJobStatusResult.Result.Blob!, altText: "Alt Text"));
            await agent.Post(post, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}