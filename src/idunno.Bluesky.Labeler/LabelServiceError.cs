// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;

namespace idunno.Bluesky.Labeler;

/// <summary>Describes a classified label-service failure and any retained diagnostics.</summary>
/// <param name="code">The classified failure code.</param>
/// <param name="message">The explanatory nonblank message.</param>
/// <param name="statusCode">The optional HTTP status code from a failed request.</param>
/// <param name="atErrorDetail">The optional AT Protocol error detail.</param>
/// <param name="exception">The optional operator diagnostic exception.</param>
/// <exception cref="ArgumentException"><paramref name="message"/> is blank, contains U+0000 or is not valid UTF-8.</exception>
/// <exception cref="ArgumentOutOfRangeException"><paramref name="code"/> is not a defined value.</exception>
/// <remarks>
/// <para><see cref="Exception"/> is an operator diagnostic and is not safe to serialize or display as a wire payload. Transports should map the error code and a sanitized message.</para>
/// <para>Messages are explanatory and are not stable discriminators. Use <see cref="Code"/> for programmatic handling.</para>
/// </remarks>
public sealed class LabelServiceError(
    LabelServiceErrorCode code,
    string message,
    HttpStatusCode? statusCode = null,
    AtErrorDetail? atErrorDetail = null,
    Exception? exception = null)
{
    /// <summary>Gets the classified error code.</summary>
    /// <value>The code callers should use for programmatic handling.</value>
    /// <exception cref="ArgumentOutOfRangeException">The error code supplied during construction is not a defined value.</exception>
    public LabelServiceErrorCode Code { get; } = Enum.IsDefined(code)
        ? code
        : throw new ArgumentOutOfRangeException(nameof(code), code, "The error code is not defined.");

    /// <summary>Gets the explanatory message.</summary>
    /// <value>The nonblank error message.</value>
    public string Message { get; } = LabelInput.ValidateText(message, nameof(message), int.MaxValue);

    /// <summary>Gets the optional HTTP status code.</summary>
    /// <value>The HTTP status, or <see langword="null"/> when the failure is not associated with an HTTP response.</value>
    public HttpStatusCode? StatusCode { get; } = statusCode;

    /// <summary>Gets the optional AT Protocol error detail.</summary>
    /// <value>The protocol error detail, or <see langword="null"/> when none is available.</value>
    public AtErrorDetail? AtErrorDetail { get; } = atErrorDetail;

    /// <summary>Gets the optional operator diagnostic exception.</summary>
    /// <value>The retained exception, or <see langword="null"/> when no exception was retained.</value>
    public Exception? Exception { get; } = exception;
}
