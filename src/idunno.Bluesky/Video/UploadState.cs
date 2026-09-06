// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Video;

/// <summary>
/// Represents the state of a multipart video upload job.
/// </summary>
public enum UploadState
{
    /// <summary>
    /// An unknown state was returned from the server and cannot be mapped to a known state. Treat as an error.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The multipart video upload job has been created and is ready to accept parts.
    /// </summary>
    Created = 1,

    /// <summary>
    /// The multipart video upload job is in the process of finishing.
    /// </summary>
    Finishing = 2,

    /// <summary>
    /// The multipart video upload job has completed successfully.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// The multipart video upload job has failed.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// The multipart video upload job has been aborted.
    /// </summary>
    Aborted = 5,

    /// <summary>
    /// The multipart video upload job has expired and is no longer valid.
    /// </summary>
    Expired = 6,
}

internal static class UploadStateExtensions
{
    /// <summary>
    /// Converts the specified string to a <see cref="UploadState"/>.
    /// </summary>
    /// <param name="uploadState">The upload state string to convert.</param>
    /// <returns>The corresponding <see cref="UploadState"/>.</returns>
    public static UploadState ToUploadState(this string uploadState)
    {
        return uploadState.ToUpperInvariant() switch
        {
            "CREATED" => UploadState.Created,
            "FINISHING" => UploadState.Finishing,
            "COMPLETED" => UploadState.Completed,
            "FAILED" => UploadState.Failed,
            "ABORTED" => UploadState.Aborted,
            "EXPIRED" => UploadState.Expired,
            _ => UploadState.Unknown,
        };
    }
}
