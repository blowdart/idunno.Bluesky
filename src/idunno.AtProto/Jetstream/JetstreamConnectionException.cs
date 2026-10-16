// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// The exception thrown when a jetstream server refuses a connection and says why.
/// </summary>
/// <remarks>
/// <para>A <see cref="JetstreamProtocolVersion.V2"/> server rejects a request it cannot serve before the web socket is
/// established, with an HTTP error and an error name such as <c>CursorTooOld</c>, <c>UnknownZstdDictionary</c> or
/// <c>InvalidRequest</c>. The web socket client does not expose that response, so it is surfaced by this exception.</para>
/// </remarks>
public sealed class JetstreamConnectionException : Exception
{
    /// <summary>
    /// Creates a new instance of <see cref="JetstreamConnectionException"/>.
    /// </summary>
    public JetstreamConnectionException() : base("The jetstream server refused the connection.")
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="JetstreamConnectionException"/> with the specified <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public JetstreamConnectionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="JetstreamConnectionException"/> with the specified <paramref name="message"/> and <paramref name="innerException"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public JetstreamConnectionException(string message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="JetstreamConnectionException"/>.
    /// </summary>
    /// <param name="statusCode">The HTTP status code the server responded with.</param>
    /// <param name="errorDetail">The error, if any, the server gave for refusing the connection.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public JetstreamConnectionException(HttpStatusCode statusCode, AtErrorDetail? errorDetail, Exception? innerException)
        : base(BuildMessage(statusCode, errorDetail), innerException)
    {
        StatusCode = statusCode;
        ErrorDetail = errorDetail;
    }

    /// <summary>
    /// Gets the HTTP status code the server responded with, if known.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Gets the error, if any, the server gave for refusing the connection.
    /// </summary>
    public AtErrorDetail? ErrorDetail { get; }

    private static string BuildMessage(HttpStatusCode statusCode, AtErrorDetail? errorDetail)
    {
        if (errorDetail?.Error is null)
        {
            return $"The jetstream server refused the connection with status {(int)statusCode}.";
        }

        return errorDetail.Message is null
            ? $"The jetstream server refused the connection with {errorDetail.Error}."
            : $"The jetstream server refused the connection with {errorDetail.Error}: {errorDetail.Message}";
    }
}