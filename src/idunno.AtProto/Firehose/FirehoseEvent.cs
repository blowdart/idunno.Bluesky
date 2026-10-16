// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Firehose;

/// <summary>
/// Encapsulates an event received from an AT Protocol event stream, such as <c>com.atproto.sync.subscribeRepos</c>
/// or <c>com.atproto.label.subscribeLabels</c>.
/// </summary>
/// <remarks>
/// <para>Match on the derived type to find what the event describes. Events a server sends which this library does not
/// recognize are surfaced as <see cref="FirehoseUnknownEvent"/>, and events which fail validation as <see cref="FirehoseInvalidEvent"/>.</para>
/// </remarks>
public abstract record FirehoseEvent
{
    /// <summary>
    /// Creates a new instance of <see cref="FirehoseEvent"/>.
    /// </summary>
    /// <param name="sequence">The sequence number of the event, if it has one.</param>
    private protected FirehoseEvent(long? sequence)
    {
        Sequence = sequence;
    }

    /// <summary>
    /// Gets the sequence number of the event, if it has one.
    /// </summary>
    /// <value>The sequence number, or <see langword="null"/> for events, such as <see cref="FirehoseInfoEvent"/>, which are not sequenced.</value>
    /// <remarks>
    /// <para>A sequence number is only meaningful to the host which issued it. Persist it after the event has been processed,
    /// and pass it as the cursor to resume the stream from the same host.</para>
    /// </remarks>
    public long? Sequence { get; }
}
