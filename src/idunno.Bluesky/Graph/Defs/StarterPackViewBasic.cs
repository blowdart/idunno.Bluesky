// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Graph;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents a basic view over a Starter Pack.
/// </summary>
public record StarterPackViewBasic : View
{
    /// <summary>
    /// Create a new instance of <see cref="StarterPackViewBasic"/>
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the starter pack.</param>
    /// <param name="cid">The <see cref="AtProto.Cid"/> of the starter pack.</param>
    /// <param name="record">The record for the starter pack.</param>
    /// <param name="creator">The <see cref="ProfileViewBasic"/> of the starter pack creator.</param>
    /// <param name="listItemCount">The number of items in the list.</param>
    /// <param name="joinedWeekCount">The number of users who joined using the list in the last week.</param>
    /// <param name="joinedAllTimeCount">The overall number of users who joined using the list.</param>
    /// <param name="labels">Labels applied to the list.</param>
    /// <param name="indexedAt">The <see cref="DateTimeOffset"/> the list was indexed at.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="cid"/>, <paramref name="record"/> or <paramref name="creator"/> is <see langword="null"/>.</exception>
    public StarterPackViewBasic(
        AtUri uri,
        Cid cid,
        StarterPack record,
        ProfileViewBasic creator,
        int listItemCount,
        int joinedWeekCount,
        int joinedAllTimeCount,
        IReadOnlyCollection<Label>? labels,
        DateTimeOffset indexedAt)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(cid);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(creator);

        Uri = uri;
        Cid = cid;

        Record = record;
        Creator = creator;
        ListItemCount = listItemCount;
        JoinedWeekCount = joinedWeekCount;
        JoinedAllTimeCount = joinedAllTimeCount;

        if (labels is not null)
        {
            Labels = new List<Label>(labels).AsReadOnly();
        }
        else
        {
            Labels = new List<Label>().AsReadOnly();
        }

        IndexedAt = indexedAt;
    }

    /// <summary>
    /// Gets the <see cref="AtUri"/> of the starter pack.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public AtUri Uri { get; init; }

    /// <summary>
    /// Gets the <see cref="AtProto.Cid"/> of the starter pack.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public Cid Cid { get; init; }

    /// <summary>
    /// Gets the <see cref="StrongReference"/> of the starter pack.
    /// </summary>
    [JsonIgnore]
    public StrongReference StrongReference => new(Uri, Cid);

    /// <summary>
    /// Gets the record for the starter pack.
    /// </summary>
    [JsonRequired]
    public StarterPack Record { get; init; }

    /// <summary>
    /// Gets the <see cref="ProfileViewBasic"/> of the list creator.
    /// </summary>
    [JsonRequired]
    public ProfileViewBasic Creator { get; init; }

    /// <summary>
    /// Gets the number of items in the starter pack.
    /// </summary>
    public int ListItemCount { get; init; }

    /// <summary>
    /// Gets the number of users that joined in the last week using the starter pack.
    /// </summary>
    public int JoinedWeekCount { get; init; }

    /// <summary>
    /// Gets the number of users that joined using the starter pack.
    /// </summary>
    public int JoinedAllTimeCount { get; init; }

    /// <summary>
    /// Gets a readonly collection of <see cref="Label"/>s applied to the starer pack view.
    /// </summary>
    public IReadOnlyCollection<Label> Labels { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> the starter pack was indexed at.
    /// </summary>
    [JsonRequired]
    public DateTimeOffset IndexedAt { get; init; }
}