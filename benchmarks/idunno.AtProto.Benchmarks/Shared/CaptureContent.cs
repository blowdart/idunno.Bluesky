// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Benchmarks;

internal static class CaptureContent
{
    /// <summary>
    /// Reads a capture body, rejecting responses larger than the configured limit.
    /// </summary>
    /// <param name="content">The response content requested without buffering.</param>
    /// <param name="maximumLength">The maximum accepted body length.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accepted body bytes.</returns>
    /// <exception cref="InvalidDataException">The response exceeds the capture limit.</exception>
    internal static async Task<byte[]> ReadAsync(HttpContent content, int maximumLength, CancellationToken cancellationToken)
    {
        using PooledContent? body = await HttpContentReader.ReadAsPooledBytes(content, maximumLength, cancellationToken).ConfigureAwait(false);
        if (body is null)
        {
            throw new InvalidDataException($"The capture response exceeds the maximum of {maximumLength} bytes.");
        }

        return body.Span.ToArray();
    }
}
