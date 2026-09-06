// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Video;

/// <summary>
/// Known video processing failure codes.
/// </summary>
public static class FailureCodes
{
    /// <summary>
    /// Validation of the video failed.
    /// </summary>
    public const string ValidationFailure = "validation_failure";

    /// <summary>
    /// Encoding of the video failed.
    /// </summary>
    public const string EncodingFailure = "encoding_failure";

    /// <summary>
    /// Uploading the video to the PDS failed.
    /// </summary>
    public const string PdsUploadFailure = "pds_upload_failure";

    /// <summary>
    /// The video is too large to upload to the PDS.
    /// </summary>
    public const string PdsUploadUnsupportedBlobSize = "pds_upload_unsupported_blob_size";

    /// <summary>
    /// A generic failure occurred.
    /// </summary>
    public const string GenericFailure = "generic_failure";
}
