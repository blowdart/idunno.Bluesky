// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Video;

/// <summary>
/// Represents the state of a multipart video upload job.
/// </summary>
public enum UploadState
{
    /// <summary>
    /// The upload state returned by the service is not a value this library knows about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The known values in a lexicon are not a closed set, so a service may introduce states this library does not yet
    /// map. This should not be treated as a terminal state on its own. The value the service actually returned is
    /// available in <see cref="UploadStatus.RawState"/> and <see cref="AbortUploadResponse.RawState"/>.
    /// </para>
    /// </remarks>
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
