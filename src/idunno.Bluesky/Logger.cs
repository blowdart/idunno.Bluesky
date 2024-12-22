// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using Microsoft.Extensions.Logging;

using idunno.AtProto;

namespace idunno.Bluesky
{
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
        internal static partial void ImageUploadSucceed(ILogger logger, Did did, string link);

        [LoggerMessage(51, LogLevel.Error, "ImageUpload for {did} with a status code {statusCode}, at error {error} message {message}")]
        internal static partial void ImageUploadFailed(ILogger logger, HttpStatusCode statusCode, Did did, string? error, string? message);

        // CreatePost logging
        [LoggerMessage(60, LogLevel.Information, "CreatePost(Post) succeeded for {did}, created {uri} / {cid}")]
        internal static partial void CreatePostWithPostSucceeded(ILogger logger, Did did, AtUri uri, Cid? cid);

        [LoggerMessage(61, LogLevel.Error, "CreatePost(Post) failed for {did} with a status code of {statusCode}, ATError {error} message {message}")]
        internal static partial void CreatePostWithPostFailed(ILogger logger, HttpStatusCode statusCode, Did did, string? error, string? message);

    }
}
