// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.AtProto.Sync;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents the streamed content of a blob retrieved from a personal data server.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="BlobContent"/> owns the underlying HTTP response. Dispose it once the <see cref="Content"/> has been read,
/// to release the connection.
/// </para>
/// </remarks>
public sealed class BlobContent : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    internal BlobContent(Stream content, Cid cid, MediaTypeHeaderValue? contentType, long? contentLength)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(cid);

        Content = content;
        Cid = cid;
        ContentType = contentType;
        ContentLength = contentLength;
    }

    /// <summary>
    /// Gets the stream containing the blob bytes.
    /// </summary>
    /// <remarks>
    /// <para>The stream reads directly from the HTTP response and is not buffered. The bytes are not verified against <see cref="Cid"/>.</para>
    /// </remarks>
    public Stream Content { get; }

    /// <summary>
    /// Gets the <see cref="AtProto.Cid"/> of the blob that was requested.
    /// </summary>
    public Cid Cid { get; }

    /// <summary>
    /// Gets the content type the personal data server reported for the blob, if any.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This value is <b>untrusted</b>. It is supplied by the personal data server and is not derived from, or checked against, the blob bytes.
    /// Do not make rendering or security decisions based on it alone. Where possible, compare it with the <c>mimeType</c> declared by the
    /// record referencing the blob, and inspect the content itself before treating it as a particular format.
    /// </para>
    /// </remarks>
    public MediaTypeHeaderValue? ContentType { get; }

    /// <summary>
    /// Gets the content length the personal data server reported for the blob, if any.
    /// </summary>
    /// <remarks>
    /// <para>This value is supplied by the personal data server and is untrusted.</para>
    /// </remarks>
    public long? ContentLength { get; }

    /// <summary>
    /// Disposes the <see cref="Content"/> stream and the underlying HTTP response.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Content.Dispose();
        }
    }

    /// <summary>
    /// Asynchronously disposes the <see cref="Content"/> stream and the underlying HTTP response.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await Content.DisposeAsync().ConfigureAwait(false);
        }
    }
}
