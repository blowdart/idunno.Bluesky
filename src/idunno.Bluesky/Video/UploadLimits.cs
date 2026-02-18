// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Video
{
    /// <summary>
    /// Encapsulates a user's restrictions placed on video uploading.
    /// </summary>
    public record UploadLimits
    {
        [JsonConstructor]
        internal UploadLimits(
            bool canUpload,
            long? remainingDailyVideos,
            long? remainingDailyBytes,
            string? error,
            string? message)
        {
            CanUpload = canUpload;
            RemainingDailyVideos = remainingDailyVideos;
            RemainingDailyBytes = remainingDailyBytes;
            Error = error;
            Message = message;
        }

        /// <summary>
        /// Gets a flag indicating whether the user can upload videos.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public bool CanUpload { get; init; }

        /// <summary>
        /// Gets the number of videos remaining the user can upload today, if a limit is imposed.
        /// </summary>
        [JsonInclude]
        public long? RemainingDailyVideos { get; init; }

        /// <summary>
        /// Gets the number of bytes remaining the user can upload today, if a limit is imposed.
        /// </summary>
        [JsonInclude]
        public long? RemainingDailyBytes { get; init; }

        /// <summary>
        /// Gets the error returned from the API, if any.
        /// </summary>
        [JsonInclude]
        public string? Error { get; init; }

        /// <summary>
        /// Gets the message returned from the API, if any.
        /// </summary>
        [JsonInclude]
        public string? Message { get; init; }
    }
}
