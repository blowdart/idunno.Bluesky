// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Describes the HTTP response to return for a <c>com.atproto.managedspace.checkAccess</c> request.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed class ManagedSpaceAccessResponse
{
    /// <summary>
    /// The media type of <see cref="Body"/>.
    /// </summary>
    public const string ContentType = "application/json";

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagedSpaceAccessResponse"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="access">The granted access for a successful response.</param>
    /// <param name="error">The XRPC error name for a failed response.</param>
    /// <param name="message">The error message for a failed response.</param>
    private ManagedSpaceAccessResponse(int statusCode, ManagedSpaceAccess? access, string? error, string? message)
    {
        StatusCode = statusCode;
        Access = access;
        Error = error;
        Message = message;

        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            if (access is not null)
            {
                writer.WriteBoolean("read", access.Read);
                writer.WriteBoolean("write", access.Write);
            }
            else
            {
                writer.WriteString("error", error);
                writer.WriteString("message", message);
            }

            writer.WriteEndObject();
        }

        Body = buffer.WrittenMemory;
    }

    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    /// <value>The status code to send.</value>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the access granted by a successful check.
    /// </summary>
    /// <value>The granted access, or <see langword="null"/> when the request failed.</value>
    public ManagedSpaceAccess? Access { get; }

    /// <summary>
    /// Gets the XRPC error name.
    /// </summary>
    /// <value>The error name, or <see langword="null"/> when the request succeeded.</value>
    public string? Error { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    /// <value>The error message, or <see langword="null"/> when the request succeeded.</value>
    public string? Message { get; }

    /// <summary>
    /// Gets the UTF-8 JSON response body.
    /// </summary>
    /// <value>The <c>{"read":…,"write":…}</c> output, or an XRPC <c>{"error":…,"message":…}</c> body.</value>
    public ReadOnlyMemory<byte> Body { get; }

    /// <summary>
    /// Gets a value that indicates whether the check succeeded.
    /// </summary>
    /// <value><see langword="true"/> if <see cref="Access"/> contains the granted access; otherwise, <see langword="false"/>.</value>
    [MemberNotNullWhen(true, nameof(Access))]
    [MemberNotNullWhen(false, nameof(Error), nameof(Message))]
    public bool Succeeded => Access is not null;

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    /// <param name="access">The granted access.</param>
    /// <returns>A 200 response containing <paramref name="access"/>.</returns>
    internal static ManagedSpaceAccessResponse Success(ManagedSpaceAccess access) =>
        new(statusCode: 200, access: access, error: null, message: null);

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="error">The XRPC error name.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A response containing an XRPC error body.</returns>
    internal static ManagedSpaceAccessResponse Failure(int statusCode, string error, string message) =>
        new(statusCode: statusCode, access: null, error: error, message: message);
}
