# Adding videos and animated GIFsto your posts

## Uploading videos

Like images, videos need to be uploaded as a blob before they can be used in a post. However, unlike images, videos undergo processing after you upload them,
and you cannot use them until processing is complete.

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
       (videoUploadResult.Result.State == idunno.Bluesky.Video.JobState.Created || videoUploadResult.Result.State == idunno.Bluesky.Video.JobState.InProgress))
{
    // Give the user some feedback
    Console.WriteLine(
      $"Video job # {videoUploadResult.Result.JobId} processing, progress {videoUploadResult.Result.Progress}");

    await Task.Delay(1000);
    videoUploadResult = await agent.GetVideoJobStatus(videoUploadResult.Result.JobId);
    videoUploadResult.EnsureSucceeded();
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

Bluesky cache processed videos for an indeterminate length of time. You may see, depending on your [logging level](logging.md),
if you have previously uploaded a video file and try to upload it again that UploadVideo() fails internally when a video has already been processed,
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
