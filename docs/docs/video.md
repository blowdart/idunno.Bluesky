# Using videos in your post
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

// Upload the video - only mp4 videos are supported by Bluesky.
var videoUploadResult = await agent.UploadVideo(
    Path.GetFileName(pathToImage),
    videoAsBytes);

// Quick fail - you'd want to be more graceful in handling errors.
videoUploadResult.EnsureSucceeded();

// Wait for processing to finish.
while (videoUploadResult.Result.State == idunno.Bluesky.Video.JobState.InProgress &&
    videoUploadResult.Succeeded)
{
    // Give the user some feedback
    Console.WriteLine($"Video job # {videoUploadResult.Result.JobId} processing, progress {videoUploadResult.Result.Progress}");

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
