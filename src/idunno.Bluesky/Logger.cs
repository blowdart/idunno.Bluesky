// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using Microsoft.Extensions.Logging;

using idunno.AtProto;

namespace idunno.Bluesky
{
    internal static partial class Logger
    {
        // GetPost
        [LoggerMessage(10, LogLevel.Error, "GetPost failed for {rKey}/ {cid} in {repo} with status code {status}, {error} {message} on {service}")]
        internal static partial void GetPostFailed(ILogger logger, HttpStatusCode status, AtIdentifier repo, RecordKey rkey, Cid? cid, string? error, string? message, Uri service);

        [LoggerMessage(11, LogLevel.Error, "GetPost succeeded for {rKey}/ {cid} in {repo} but returned a null result from {service}")]
        internal static partial void GetPostSucceededButReturnedNullResult(ILogger logger, AtIdentifier repo, RecordKey rkey, Cid? cid, Uri service);

        // Actions
        [LoggerMessage(20, LogLevel.Error, "Quote() succeeded but the results count from ApplyWrites was {count}.")]
        internal static partial void QuoteCreateSucceededButResultResultsIsNotCountOne(ILogger logger, int count);

        [LoggerMessage(21, LogLevel.Error, "Quote() succeeded but the results ApplyWrites was of type {type}.")]
        internal static partial void QuoteCreateSucceededButReturnResultUnexpectedType(ILogger logger, Type type);
    }
}
