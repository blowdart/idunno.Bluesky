// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Actor;

/// <summary>
/// Provides a view over an actor's current status
/// </summary>
public sealed record StatusView
{
    /// <summary>
    /// Creates a new instance of <see cref="StatusView"/>.
    /// </summary>
    /// <param name="status">The status for the account.</param>
    /// <param name="record">A record associated with the <paramref name="status" />.</param>
    /// <param name="embed">An <see cref="EmbeddedView"/> associated with the <paramref name="status" />, if any.</param>
    /// <param name="labels">A collection of <see cref="Label"/>s associated with the <paramref name="status" />, if any.</param>
    /// <param name="expiresAt">A <see cref="DateTimeOffset" /> when this <paramref name="status" /> will expire, if any.</param>
    /// <param name="isActive">A flag indicating whether the <paramref name="status" /> has not expired. Only present when <paramref name="expiresAt"/> was set.</param>
    /// <param name="isDisabled">A flag indicating whether the <paramref name="status" /> has been disabled by the service.</param>
    /// <param name="uri">The <see cref="AtUri"/> of the <paramref name="status" />, if any.</param>
    /// <param name="cid">The <see cref="AtProto.Cid" /> of the <paramref name="status" />, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="status"/> or <paramref name="record"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public StatusView(
        string status,
        JsonNode record,
        EmbeddedView? embed,
        IReadOnlyCollection<Label>? labels,
        DateTimeOffset? expiresAt,
        bool? isActive,
        bool? isDisabled,
        AtUri? uri,
        Cid? cid)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(record);

        Status = status;
        Record = record;
        Embed = embed;
        Labels = labels ?? [];
        ExpiresAt = expiresAt;
        IsActive = isActive;
        IsDisabled = isDisabled;
        Uri = uri;
        Cid = cid;
    }

    /// <summary>
    /// Gets the status for the account.
    /// </summary>
    [JsonRequired]
    public string Status { get; init; }

    /// <summary>
    /// Gets the record associated with the <see cref="Status"/>.
    /// </summary>
    [JsonRequired]
    public JsonNode Record { get; init; }

    /// <summary>
    /// Gets the <see cref="EmbeddedView"/> associated with the <see cref="Status"/>, if any.
    /// </summary>
    public EmbeddedView? Embed { get; init; }

    /// <summary>
    /// Gets the collection of <see cref="Label"/>s associated with the <see cref="Status"/>, if any.
    /// </summary>
    public IReadOnlyCollection<Label> Labels
    {
        get;

        init => field = value is null ? [] : new List<Label>(value).AsReadOnly();
    }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the <see cref="Status"/> will expire, if any.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the <see cref="Status"/> has not expired.
    /// Only present when <see cref="ExpiresAt"/> was set.
    /// </summary>
    public bool? IsActive { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the <see cref="Status"/> has been disabled by the service.
    /// </summary>
    public bool? IsDisabled { get; init; }

    /// <summary>
    /// Gets the <see cref="AtUri"/> of the <see cref="Status"/>, if any.
    /// </summary>
    public AtUri? Uri { get; init; }

    /// <summary>
    /// Gets the <see cref="AtProto.Cid"/> of the <see cref="Status"/>, if any.
    /// </summary>
    public Cid? Cid { get; init; }
}
