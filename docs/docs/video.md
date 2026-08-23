# Adding videos and animated GIFs to your posts

## Uploading videos

Like images, videos need to be uploaded as a blob before they can be used in a post. However, unlike images, videos undergo processing after you upload them,
and you cannot use them until processing is complete.

For small video uploads you can use the `UploadVideo()` method.

```c#

// Read the video from a file into a byte array
byte[] videoAsBytes;
using (FileStream fs = File.OpenRead(pathToImage))
using (MemoryStream ms = new())
{
    fs.CopyTo(memoryStream);
    videoAsBytes = memoryStream.ToArray();
}

// Upload the video
var videoUploadResult = await agent.UploadVideo(
    fileName:Path.GetFileName(pathToImage),
    video:videoAsBytes,
    mimeType: "video/mp4");

// Quick fail - you'd want to be more graceful in handling errors.
videoUploadResult.EnsureSucceeded();

// Wait for processing to finish.
while (videoUploadResult.Succeeded &&
       videoUploadResult.Result.State != JobState.Completed &&
       videoUploadResult.Result.State != JobState.Failed && 
       videoUploadResult.Result.State != JobState.Unknown)
{
    // Give the user some feedback
    Console.WriteLine(
      $"Video job # {videoUploadResult.Result.JobId} processing, progress {videoUploadResult.Result.Progress}");

    await Task.Delay(1000);
    videoUploadResult = await agent.GetJobStatus(videoUploadResult.Result.JobId);
    videoUploadResult.EnsureSucceeded();
}

if (videoUploadResult.Result.State == JobState.Unknown)
{
    // Bluesky returned a status that's not part of the published lexicon. This should be treated as an error.
    return;
}

if (!videoUploadResult.Succeeded ||
    videoUploadResult.Result.Blob is null ||
    videoUploadResult.Result.State != idunno.Bluesky.Video.JobState.Completed)
{
    // Handle the error
    return;
}

EmbeddedVideo video = new(videoUploadResult.Result.Blob!, altText: "Alt Text");
```

The [Samples.Video](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.Video) project shows the above code in action,
and demonstrates how to use the resulting `EmbeddedVideo` in a post.

## Large video uploads with partial uploads

For larger video uploads you must use the partial upload functionality, using `StartUpload()`, `UploadPart()` and `FinishUpload()`.
This set of APIs allows you to upload a video in multiple parts, which is useful for large videos, and the uploading of individual parts
can be done in parallel. Alex from the Bluesky team discusses this in an [implementation guide](https://alex-bsky.leaflet.pub/3mthoelgvrs2h).

To use partial uploads first call `StartUpload()` to get a jobId, the number of parts to upload, and the size for each part.
Using the size for each part split your video into individual parts, with the final part being the remainder after splitting the video using the part size.
Next call `UploadPart()` for each part using the jobID from `StartUpload()`. If the part uploads succeed call `FinishUpload()` to complete the upload.

>[!IMPORTANT]
> If, at any point in the partial upload process, you encounter an error or a failure, you should must call `AbortUpload()` to clean up the upload job,
> and clear the upload reservations on the server. Failing to do so will result in the user's upload limits being lowered by the full size of the failed upload.

```c#
// Get information about the file to upload.
string filePath = "sample.mp4";

if (!File.Exists(filePath))
{
    Console.WriteLine($"❌ File {filePath} does not exist.");
    return;
}

var fileInfo = new FileInfo(filePath);
if (fileInfo.Length > int.MaxValue)
{
    Console.WriteLine($"❌ File {filePath} is too large to upload. Max size is {int.MaxValue} bytes.");
    return;
}

int fileSize = (int)fileInfo.Length;


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
            await sourceStream.ReadExactlyAsync(partBytes.AsMemory(0, partSize), ct).ConfigureAwait(false);

            Console.WriteLine($"💾 Uploading part {partNumber} for jobID {jobId} with size {partBytes.Length} bytes.");
            uploadPartResponses[uploadPart] = await agent.UploadPart(
                jobId: jobId,
                part: partNumber,
                bytes: partBytes[0..partSize],
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
```

The [Samples.PartialUploads](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.PartialUploads) project shows the above code in action.

Bluesky cache processed videos for an indeterminate length of time. You may see, depending on your [logging level](logging.md),
if you have previously uploaded a video file and try to upload it again that `UploadVideo()` fails internally when a video has already been processed,
but returns a succeeded result with the job status of the previous upload.

## Video Captions
If you have captions they will also need to be uploaded. You will need to specify the language for your captions when calling `UploadCaptions`.
Once the captions have been uploaded you specify the caption blob when creating a new instance of EmbeddedVideo. You can specify multiple caption files
if you have captions in different languages.

```c#
// Read the captions from a file into a byte array
byte[] captionsAsBytes;
using (FileStream fs = File.OpenRead(pathToImage))
using (MemoryStream ms = new())
{
    fs.CopyTo(memoryStream);
    captionsAsBytes = memoryStream.ToArray();
}

var captionUploadResult =
  await agent.UploadCaptions(captionsAsBytes, "en")

// Quick fail - you'd want to be more graceful in handling errors.
captionUploadResult.EnsureSucceeded();

EmbeddedVideo video = new(
    videoUploadResult.Result.Blob,
    altText: "Alt Text",
    captions: captionUploadResult.Result);

var postResult = await agent.Post("With video and captions", video: video);
```

The [Video sample](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.Video) demonstrates how to put it all together.

## Using videos with PostBuilder
If you are using a `PostBuilder` you can use `PostBuilder.Add()` to add an instance of `EmbeddedVideo`.

> [!IMPORTANT]
> Videos and images are mutually exclusive. You cannot have both images and videos in a post.
> If you add a video to a `PostBuilder` it will remove any images, if you add images to a `PostBuilder`
> any attached video will be removed.

## Checking your upload limits

Bluesky imposes limits outside the normal rate limitations on video.

To check if you have the ability to upload video, and how many videos or bytes you have left call `agent.GetVideoUploadLimits()`
and validate you have enough quota left:

```c#
var videoUploadLimitsResult = await agent.GetVideoUploadLimits(cancellationToken: cancellationToken);
videoUploadLimitsResult.EnsureSucceeded();

if (!videoUploadLimitsResult.Result.CanUpload ||
    videoUploadLimitsResult.Result.RemainingDailyVideos == 0 ||
    videoUploadLimitsResult.Result.RemainingDailyBytes < (ulong)videoAsBytes.LongLength)
    {
         // You can't upload the video stream in videoAsBytes, react accordingly.
    }
```

## Animated GIFs

Bluesky treats animated GIFs as videos, so you can upload them in the same way as videos. To upload an animated GIF using
the `BlueskyAgent` use `UploadVideo()` and specify the mime type as `image/gif`. The same status polling process applies to animated GIFs as they do to videos.

Bluesky treats animated GIFs as videos, so you must create an instance of `EmbeddedVideo` to use them in a post. Do not use `EmbeddedImage` for animated GIFs.
