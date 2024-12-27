# <a name="posting">Using videos in your post</a>

Like images, videos need to be uploaded as a blob before they can be used in a post. Unlike images videos undergo processing after you upload them,
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
    Path.GetFileName(pathToImage),
    videoAsBytes);

// Quick fail - you'd want to be more graceful in handling errors.
videoUploadResult.EnsureSucceeded();

// Wait for processing
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

If you have captions they will also need to be uploaded. You will need to specify the language for your captions when calling `UploadCaptions`.
Once the captions have been uploaded you specify the caption blob when creating a new instance of EmbeddedVideo.

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

EmbeddedVideo video = new(videoUploadResult.Result.Blob, altText: "Alt Text", captions: captionUploadResult.Result);

var postResult = await agent.Post("With video and captions", video: video);
```
