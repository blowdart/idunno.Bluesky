// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.Bluesky.Video;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

internal static partial class Logger
{
    // GetPostRecord
    [LoggerMessage(10, LogLevel.Error, "GetPostRecord failed for {uri}/ {cid} with status code {status}, {error} {message} on {service}")]
    internal static partial void GetPostRecordFailed(ILogger logger, HttpStatusCode status, AtUri uri, Cid? cid, string? error, string? message, Uri service);

    [LoggerMessage(11, LogLevel.Error, "GetPostRecord returned OK for {uri}/ {cid} but returned a null result from {service}")]
    internal static partial void GetPostRecordSucceededButReturnedNullResult(ILogger logger, AtUri uri, Cid? cid, Uri service);

    [LoggerMessage(12, LogLevel.Information, "GetPostRecord succeeded for {uri} / {cid} from {service} for {did}")]
    internal static partial void GetPostRecordSucceeded(ILogger logger, Did did, AtUri uri, Cid? cid, Uri service);

    [LoggerMessage(13, LogLevel.Information, "Anonymous GetPostRecord succeeded for {uri} / {cid} from {service}")]
    internal static partial void GetPostRecordSucceededAnon(ILogger logger, AtUri uri, Cid? cid, Uri service);

    // Actions
    [LoggerMessage(20, LogLevel.Error, "Quote() succeeded but the results count from ApplyWrites was {count}.")]
    internal static partial void QuoteCreateSucceededButResultResultsIsNotCountOne(ILogger logger, int count);

    [LoggerMessage(21, LogLevel.Error, "Quote() succeeded but the results ApplyWrites was of type {type}.")]
    internal static partial void QuoteCreateSucceededButReturnResultUnexpectedType(ILogger logger, Type type);

    [LoggerMessage(25, LogLevel.Error, "Follow() failed as {handle} could not be resolved to a DID.")]
    internal static partial void FollowFailedAsHandleCouldNotResolve(ILogger logger, Handle handle);

    [LoggerMessage(26, LogLevel.Information, "Follow() succeeded {user} is now following {followed}.")]
    internal static partial void FollowSucceeded(ILogger logger, Did user, Did followed);

    [LoggerMessage(27, LogLevel.Error, "Follow() failed for {user}, did not follow {followed}.")]
    internal static partial void FollowFailedAtApiLayer(ILogger logger, Did user, Did followed);

    [LoggerMessage(30, LogLevel.Error, "Unfollow() failed as {handle} could not be resolved to a DID.")]
    internal static partial void UnfollowFailedAsHandleCouldNotResolve(ILogger logger, Handle handle);

    [LoggerMessage(31, LogLevel.Information, "Unfollow() failed as could not get user profile for {did}.")]
    internal static partial void UnfollowFailedAsHandleCouldNotGetUserProfile(ILogger logger, Did did);

    [LoggerMessage(32, LogLevel.Information, "Unfollow() failed as current user is not following {did}.")]
    internal static partial void UnfollowFailedAsHandleCouldNotGetUserIsNotFollowing(ILogger logger, Did did);

    [LoggerMessage(35, LogLevel.Error, "Block() failed as {handle} could not be resolved to a DID.")]
    internal static partial void BlockFailedAsHandleCouldNotResolve(ILogger logger, Handle handle);

    [LoggerMessage(40, LogLevel.Information, "Unblock() failed as could not get user profile for {did}.")]
    internal static partial void UnblockFailedAsHandleCouldNotGetUserProfile(ILogger logger, Did did);

    [LoggerMessage(41, LogLevel.Error, "Unblock() failed as current user is not following {did}.")]
    internal static partial void UnblockFailedAsHandleCouldNotGetUserIsNotFollowing(ILogger logger, Did did);

    [LoggerMessage(45, LogLevel.Debug, "CreatePostWithGatesSucceeded for {did} with a record key of {recordKey}")]
    internal static partial void CreatePostWithGatesSucceeded(ILogger logger, RecordKey recordKey, Did did);

    [LoggerMessage(46, LogLevel.Error, "CreatePostWithGatesFailed for {did} with a status code {statusCode}, ATError {error} message {message}")]
    internal static partial void CreatePostWithGatesFailed(ILogger logger, HttpStatusCode statusCode, Did did, string? error, string? message);

    [LoggerMessage(50, LogLevel.Debug, "ImageUpload succeeded for {did} {link}")]
    internal static partial void ImageUploadSucceed(ILogger logger, Did did, Cid link);

    [LoggerMessage(51, LogLevel.Error, "ImageUpload for {did} with a status code {statusCode}, at error {error} message {message}")]
    internal static partial void ImageUploadFailed(ILogger logger, HttpStatusCode statusCode, Did did, string? error, string? message);

    [LoggerMessage(55, LogLevel.Error, "GetServiceAuth for video failed failed with {statusCode} when getting service auth token for {did}, error {error} message {message}")]
    internal static partial void UploadVideoServiceTokenAcquisitionFailed(ILogger logger, HttpStatusCode statusCode, Did did, string? error, string? message);

    // CreatePost logging
    [LoggerMessage(60, LogLevel.Information, "CreatePost(Post) succeeded for {did}, created {uri} / {cid}")]
    internal static partial void CreatePostWithPostSucceeded(ILogger logger, Did did, AtUri uri, Cid? cid);

    [LoggerMessage(61, LogLevel.Error, "CreatePost(Post) failed for {did} with a status code of {statusCode}, ATError {error} message {message}")]
    internal static partial void CreatePostWithPostFailed(ILogger logger, HttpStatusCode statusCode, Did did, string? error, string? message);

    // Upload video logging
    [LoggerMessage(70, LogLevel.Information, "UploadVideo succeeded for {did} with job #{jobId}.")]
    internal static partial void UploadVideoSucceeded(ILogger logger, string jobId, Did did);

    [LoggerMessage(71, LogLevel.Error, "UploadVideo failed with {statusCode} when uploading video for {did}, error {error} message {message}")]
    internal static partial void UploadVideoFailed(ILogger logger, HttpStatusCode statusCode, Did did, string? error, string? message);

    [LoggerMessage(72, LogLevel.Information, "UploadVideo started for {did} on {server}, filename: {fileName} length: {length}")]
    internal static partial void UploadVideoStarted(ILogger logger, Did did, Uri server, string fileName, long length);

    [LoggerMessage(74, LogLevel.Error, "GetServerDescription in UploadVideo for user {did}, service {service} failed with {statusCode} error {error} message {message}")]
    internal static partial void UploadVideoGetServerDescriptionFailed(ILogger logger, Did did, Uri service, HttpStatusCode statusCode, string? error, string? message);

    [LoggerMessage(75, LogLevel.Debug, "GetUploadLimitsSucceeded succeeded for {did} CanUpload = {canUpload}, RemainingDailyVideos = {remainingDailyVideos} RemainingDailyBytes: {remainingDailyBytes}")]
    internal static partial void GetUploadLimitsSucceeded(ILogger logger, Did did, bool canUpload, long? remainingDailyVideos, long? remainingDailyBytes);

    [LoggerMessage(76, LogLevel.Error, "GetUploadLimitsSucceeded failed with {statusCode} for {did}, error {error} message {message}")]
    internal static partial void GetUploadLimitsFailed(ILogger logger, HttpStatusCode statusCode, Did did, string? error, string? message);

    [LoggerMessage(80, LogLevel.Information, "GetJobStatus for jobId {jobId} succeeded, state is {state}, progress {progress}")]
    internal static partial void GetJobStatusSucceeded(ILogger logger, string jobId, JobState state, int? progress);

    [LoggerMessage(81, LogLevel.Error, "GetJobStatus failed with {statusCode} error {error} message {message}")]
    internal static partial void GetJobStatusFailed(ILogger logger, HttpStatusCode statusCode, string? error, string? message);

    [LoggerMessage(90, LogLevel.Error, "GetLabelerServices failed because GetUserPreferencesFailed with {statusCode} error {error} message {message}")]
    internal static partial void GetUserPreferencesFailedInGetLabelerServices(ILogger logger, HttpStatusCode statusCode, string? error, string? message);

    [LoggerMessage(100, LogLevel.Information, "Uploading image {fileName} from draft {draftId}")]
    internal static partial void UploadingImageFromDraft(ILogger logger, string fileName, TimestampIdentifier draftId);

    [LoggerMessage(101, LogLevel.Information, "Uploading video {fileName} from draft {draftId}")]
    internal static partial void UploadingVideoFromDraft(ILogger logger, string fileName, TimestampIdentifier draftId);

    [LoggerMessage(102, LogLevel.Information, "Uploading caption {fileName} from draft {draftId}")]
    internal static partial void UploadingCaptionFromDraft(ILogger logger, string fileName, TimestampIdentifier draftId);

    [LoggerMessage(103, LogLevel.Error, "DeleteDraft failed for {draftId} with status code {statusCode} error {error} message {message}")]
    internal static partial void DeleteDraftFailed(ILogger logger, TimestampIdentifier draftId, HttpStatusCode statusCode, string? error, string? message);

    [LoggerMessage(105, LogLevel.Information, "UploadMedia succeeded for {did} with job #{jobId}.")]
    internal static partial void UploadMediaSucceeded(ILogger logger, string jobId, Did did);

    [LoggerMessage(106, LogLevel.Error, "UploadMedia failed with {statusCode} when uploading media for {did}, error {error} message {message}")]
    internal static partial void UploadMediaFailed(ILogger logger, HttpStatusCode statusCode, Did did, string? error, string? message);

    [LoggerMessage(107, LogLevel.Information, "UploadMedia started for {did} on {server}, filename: {fileName} length: {length} mimeType: {mimeType}")]
    internal static partial void UploadMediaStarted(ILogger logger, Did did, Uri server, string fileName, long length, string mimeType);

    [LoggerMessage(108, LogLevel.Error, "GetServerDescription in UploadMedia for user {did}, service {service} failed with {statusCode} error {error} message {message}")]
    internal static partial void UploadMediaGetServerDescriptionFailed(ILogger logger, Did did, Uri service, HttpStatusCode statusCode, string? error, string? message);

    // Card Generator errors
    [LoggerMessage(110, LogLevel.Error, "Failed to upload embedded card image for {url} with status code {statusCode}, error {error} message {message}")]
    internal static partial void EmbeddedCardImageUploadFailed(ILogger logger, Uri url, HttpStatusCode statusCode, string? error, string? message);

    [LoggerMessage(111, LogLevel.Error, "Exception thrown when uploading embedded card image for {url}")]
    internal static partial void EmbeddedCardImageUploadThrew(ILogger logger, Uri url, Exception ex);

    [LoggerMessage(112, LogLevel.Error, "Failed to get OpenGraph data for {url} with status code {statusCode}")]
    internal static partial void GetOpenGraphDataFailed(ILogger logger, Uri url, HttpStatusCode statusCode);

    [LoggerMessage(113, LogLevel.Information, "Retrieval of {url} failed with {statusCode}")]
    internal static partial void EmbeddedCardGetRequestFailedWithStatusCode(ILogger logger, Uri url, HttpStatusCode statusCode);

    [LoggerMessage(114, LogLevel.Error, "Exception thrown when getting web page {url}")]
    internal static partial void EmbeddedCardGetRequestThrew(ILogger logger, Uri url, Exception ex);

    [LoggerMessage(115, LogLevel.Error, "Failed to find or parse site.standard.document link for {url}")]
    internal static partial void FailedToFindOrParseSiteStandardDocumentLink(ILogger logger, Uri url);

    [LoggerMessage(116, LogLevel.Error, "Author DID/repo is not present in the site.standard.document link {atUri} for {url}")]
    internal static partial void AuthorDidRepoNotPresentInSiteStandardDocumentLink(ILogger logger, AtUri atUri, Uri url);

    [LoggerMessage(117, LogLevel.Error, "PDS for author DID {did} not found for {url}")]
    internal static partial void PdsForAuthorDidNotFound(ILogger logger, Did did, Uri url);

    [LoggerMessage(118, LogLevel.Error, "Publication DID/repo is not present in the site.standard.publication link {atUri} for {url} nor in well-known location")]
    internal static partial void PublicationDidRepoNotPresent(ILogger logger, AtUri atUri, Uri url);

    [LoggerMessage(119, LogLevel.Error, "PDS for publication DID {did} not found for {url}")]
    internal static partial void PdsForPublicationDidNotFound(ILogger logger, Did did, Uri url);

    [LoggerMessage(120, LogLevel.Error, "Exception thrown when getting image from {url}")]
    internal static partial void EmbeddedCardImageGetRequestThrew(ILogger logger, Uri url, Exception ex);

    [LoggerMessage(121, LogLevel.Error, "Author DID resolution failed for {atUri} when processing {uri}")]
    internal static partial void AuthorDidResolutionFailed(ILogger logger, Uri uri, AtUri atUri);

    [LoggerMessage(122, LogLevel.Debug, "{uri} returned a file too small to be an image")]
    internal static partial void EmbeddedCardImageTooSmall(ILogger logger, Uri uri);

    [LoggerMessage(123, LogLevel.Debug, "Content for {uri} is not recognized as an image")]
    internal static partial void EmbeddedCardImageTypeNotRecognized(ILogger logger, Uri uri);

    [LoggerMessage(124, LogLevel.Error, "Image from {url} is too large, {contentLength} bytes. Maximum allowed size is {maxSize} bytes.")]
    internal static partial void EmbeddedCardImageTooLarge(ILogger logger, Uri url, long contentLength, long maxSize);

    [LoggerMessage(125, LogLevel.Error, "Could not delete temporary file {fileName}")]
    internal static partial void CouldNotDeleteTemporaryFile(ILogger logger, string fileName, Exception ex);
}