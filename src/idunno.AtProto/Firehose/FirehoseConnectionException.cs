// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

namespace idunno.AtProto.Firehose;

/// <summary>
/// The exception thrown when a firehose server refuses a connection, or ends a subscription with an error.
/// </summary>
/// <remarks>
/// <para>A server can refuse the web socket upgrade with an HTTP error, which is reported in <see cref="StatusCode"/>, or
/// send an error frame such as <c>FutureCursor</c> and close the connection, which is reported in <see cref="ErrorDetail"/>.</para>
/// <para>The error and message come from the server, so they are untrusted. They are only lightly sanitized, stripped of control
/// and bidirectional formatting characters and truncated, and must not be treated as trusted input. Encode them for the context
/// they are used in, such as HTML, and do not use them to make security decisions.</para>
/// </remarks>
public sealed class FirehoseConnectionException : Exception
{
    /// <summary>
    /// Creates a new instance of <see cref="FirehoseConnectionException"/>.
    /// </summary>
    public FirehoseConnectionException() : base("The firehose server refused the connection.")
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="FirehoseConnectionException"/> with the specified <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public FirehoseConnectionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="FirehoseConnectionException"/> with the specified <paramref name="message"/> and <paramref name="innerException"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public FirehoseConnectionException(string message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="FirehoseConnectionException"/>.
    /// </summary>
    /// <param name="statusCode">The HTTP status code the server responded with, if the connection was refused before the web socket was established.</param>
    /// <param name="errorDetail">The error, if any, the server gave.</param>
    /// <param name="retryAfter">How long the server asked the client to wait before retrying, if it said.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public FirehoseConnectionException(HttpStatusCode? statusCode, AtErrorDetail? errorDetail, TimeSpan? retryAfter, Exception? innerException)
        : base(BuildMessage(statusCode, errorDetail), innerException)
    {
        StatusCode = statusCode;
        ErrorDetail = errorDetail;
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Creates a new instance of <see cref="FirehoseConnectionException"/> with a message of the reader's own, rather than one built from the status.
    /// </summary>
    /// <param name="statusCode">The HTTP status code the server responded with, if there was one to report.</param>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    internal FirehoseConnectionException(HttpStatusCode? statusCode, string message, Exception? innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the HTTP status code the server responded with, if the connection was refused before the web socket was established.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Gets the error, if any, the server gave.
    /// </summary>
    public AtErrorDetail? ErrorDetail { get; }

    /// <summary>
    /// Gets how long the server asked the client to wait before retrying, if it said.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    private static string BuildMessage(HttpStatusCode? statusCode, AtErrorDetail? errorDetail)
    {
        if (errorDetail?.Error is null)
        {
            return statusCode is null
                ? "The firehose server ended the subscription with an error."
                : $"The firehose server refused the connection with status {(int)statusCode}.";
        }

        return errorDetail.Message is null
            ? $"The firehose server returned {errorDetail.Error}."
            : $"The firehose server returned {errorDetail.Error}: {errorDetail.Message}";
    }
}
