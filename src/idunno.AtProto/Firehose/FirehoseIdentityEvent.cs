// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Firehose;

/// <summary>
/// Encapsulates an <c>#identity</c> event, which says an account's identity may have changed.
/// </summary>
/// <remarks>
/// <para>The event is a hint to refresh any cached identity data for <see cref="Did"/>. It is not proof of a change,
/// so resolve the DID and handle yourself rather than trusting <see cref="Handle"/>.</para>
/// </remarks>
public sealed record FirehoseIdentityEvent : FirehoseEvent
{
    /// <summary>
    /// Creates a new instance of <see cref="FirehoseIdentityEvent"/>.
    /// </summary>
    /// <param name="sequence">The sequence number of the event.</param>
    /// <param name="did">The DID of the account.</param>
    /// <param name="time">When the event was emitted.</param>
    /// <param name="handle">The handle the server reported, if any.</param>
    internal FirehoseIdentityEvent(long sequence, Did did, DateTimeOffset time, string? handle) : base(sequence)
    {
        Did = did;
        Time = time;
        Handle = handle;
    }

    /// <summary>
    /// Gets the DID of the account.
    /// </summary>
    public Did Did { get; }

    /// <summary>
    /// Gets when the event was emitted by the upstream server.
    /// </summary>
    public DateTimeOffset Time { get; }

    /// <summary>
    /// Gets the handle the server reported for the account, if any.
    /// </summary>
    /// <value>The reported handle, which may be <c>handle.invalid</c>, or <see langword="null"/> if none was sent.</value>
    /// <remarks>
    /// <para>The handle may be passed through from upstream without validation, so it is untrusted and is exposed as a string.</para>
    /// </remarks>
    public string? Handle { get; }
}
