// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Firehose;

/// <summary>
/// Decodes the payloads of a single event stream endpoint into typed events.
/// </summary>
internal interface IFirehosePayloadDecoder
{
    /// <summary>
    /// Gets the NSID of the subscription endpoint.
    /// </summary>
    string Nsid { get; }

    /// <summary>
    /// Gets a value that indicates whether messages of <paramref name="type"/> are known and carry a sequence number.
    /// </summary>
    /// <param name="type">The short message type, such as <c>#commit</c>.</param>
    /// <returns><see langword="true"/> if the type is known and sequenced; otherwise, <see langword="false"/>.</returns>
    bool IsSequenced(string type);

    /// <summary>
    /// Reads the DID a message claims to be about, for reporting an invalid event.
    /// </summary>
    /// <param name="type">The short message type.</param>
    /// <param name="fields">The payload fields.</param>
    /// <returns>The DID, or <see langword="null"/> if there is none or it cannot be read.</returns>
    Did? GetSubject(string type, CborFields fields);

    /// <summary>
    /// Decodes a sequenced message.
    /// </summary>
    /// <param name="type">The short message type.</param>
    /// <param name="sequence">The validated sequence number.</param>
    /// <param name="fields">The payload fields.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The decoded event.</returns>
    /// <exception cref="InvalidDataException">The message fails validation.</exception>
    Task<FirehoseEvent> DecodeAsync(string type, long sequence, CborFields fields, CancellationToken cancellationToken);
}
