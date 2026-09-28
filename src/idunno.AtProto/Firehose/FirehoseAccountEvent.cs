// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Sync;

namespace idunno.AtProto.Firehose;

/// <summary>
/// Encapsulates an <c>#account</c> event, which describes a change to an account's hosting status.
/// </summary>
public sealed record FirehoseAccountEvent : FirehoseEvent
{
    /// <summary>
    /// Creates a new instance of <see cref="FirehoseAccountEvent"/>.
    /// </summary>
    /// <param name="sequence">The sequence number of the event.</param>
    /// <param name="did">The DID of the account.</param>
    /// <param name="time">When the event was emitted.</param>
    /// <param name="active">Whether the account is active.</param>
    /// <param name="status">The reason the account is inactive, if any.</param>
    internal FirehoseAccountEvent(long sequence, Did did, DateTimeOffset time, bool active, RepoStatus? status) : base(sequence)
    {
        Did = did;
        Time = time;
        Active = active;
        Status = status;
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
    /// Gets a value that indicates whether the account's repository is active on the host.
    /// </summary>
    public bool Active { get; }

    /// <summary>
    /// Gets the reason the account is not active, if the server gave one.
    /// </summary>
    /// <value>The account status, <see cref="RepoStatus.Unknown"/> for a status this library does not recognize, or <see langword="null"/> if none was sent.</value>
    public RepoStatus? Status { get; }
}
