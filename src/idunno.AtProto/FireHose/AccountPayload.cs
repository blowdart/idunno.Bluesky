// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using PeterO.Cbor;

namespace idunno.AtProto.FireHose;

/// <summary>
/// Represents a change to an account's <paramref name="Status" /> on a host (eg, PDS or Relay).
/// The semantics of this event are that the <paramref name="Status" /> is at the host which emitted the event,
/// not necessarily that at the currently active PDS.
/// For example a Relay takedown would emit a takedown with <paramref name="Active" />=<see langword="false" />, even if the PDS is still active.
/// </summary>
/// <param name="Seq">The stream sequence number of this message.</param>
/// <param name="Did">The account this event corresponds to.</param>
/// <param name="Time">The <see cref="DateTimeOffset"/> when this message was originally broadcast.</param>
/// <param name="Active">Flag indicating whether the account has a repository which can be fetched from the host that emitted this event.</param>
/// <param name="Status">If <paramref name="Active"/> is <see langword="false"/>, indicates a reason for why the account is not active</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Documentation", "CSENSE020:Potential ghost parameter reference in documentation", Justification = "Not ghost references.")]
public record AccountPayload(long Seq, Did Did, DateTimeOffset Time, bool Active, string? Status) : IFramePayload, ICborConvertor<AccountPayload>
{
    internal AccountPayload(CBORObject cborObject) : this(
        cborObject["seq"].AsInt64Value(),
        new Did(cborObject["did"].AsString()),
        cborObject["time"].ToDateTimeOffset(),
        cborObject["active"].AsBoolean(),
        cborObject["status"]?.AsString())
    {
    }

    /// <summary>
    /// Gets the stream sequence for the message.
    /// </summary>
    public long Seq { get; init; } = Seq;

    /// <summary>
    /// Gets the <see cref="AtProto.Did"/> the event corresponds to.
    /// </summary>
    public Did Did { get; init; } = Did;

    /// <summary>
    /// The <see cref="DateTimeOffset"/> when the message was originally broadcast.
    /// </summary>
    public DateTimeOffset Time { get; init; } = Time;

    /// <summary>
    /// Gets a flag indicating whether the account has a repository which can be fetched from the host that emitted this event.
    /// </summary>
    public bool Active { get; init; } = Active;

    /// <summary>
    /// Gets the reason why an account is not active. Will only be present if <see cref="Active"/> is <see langword="false"/>.
    /// </summary>
    public string? Status { get; init; } = Status;

    /// <inheritdoc/>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="cborObject"/> is <see langword="null" />.</exception>
    public static AccountPayload FromCBORObject(CBORObject cborObject)
    {
        ArgumentNullException.ThrowIfNull(cborObject);

        return new AccountPayload(cborObject);
    }
}
