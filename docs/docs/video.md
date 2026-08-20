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
// Read a sample video file. You can change this to any video file you want to upload.
string filePath = "mp4-99mb-sample.mp4";

// Read the entire video file into memory. This is not recommended for large files, but is done here for simplicity.
byte[] video = await File.ReadAllBytesAsync(filePath, cancellationToken);

// Check the authenticated user has the ability to upload a video of this size.
var getVideoUploadLimitsResult = await agent.GetVideoUploadLimits(cancellationToken: cancellationToken).ConfigureAwait(false);
if (!getVideoUploadLimitsResult.Succeeded)
{
    Console.WriteLine($"Failed to get video upload limits. Server returned {getVideoUploadLimitsResult.StatusCode} / {getVideoUploadLimitsResult.AtErrorDetail?.Error} / {getVideoUploadLimitsResult.AtErrorDetail?.Message}");
    return;
}

if (getVideoUploadLimitsResult.Result.RemainingDailyVideos == 0)
{
    Console.WriteLine($"No remaining daily video uploads.");
    return;
}

if (getVideoUploadLimitsResult.Result.RemainingDailyBytes < video.Length)
{
    Console.WriteLine($"Video file is too large to upload. Max size is {getVideoUploadLimitsResult.Result.RemainingDailyBytes} bytes, but the video file is {video.Length} bytes.");
    return;
}

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

// Local function to upload a part. This is defined here it can access the uploadPartResponses array.
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

Bluesky treats animated GIFs as videos, so you can upload them in the same way as videos. To make animated GIF support more discoverable,
the `BlueskyAgent` has a convenience method `UploadAnimatedGif()`. Alternatively, you can use `UploadVideo()` and specify the mime type as `image/gif`.
The same processing rules apply to animated GIFs as they do to videos.

Bluesky treats animated GIFs as videos, so you must create an instance of `EmbeddedVideo` to use them in a post. You cannot use `EmbeddedImage` for animated GIFs.
