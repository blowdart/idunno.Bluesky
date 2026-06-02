// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Actor;

/// <summary>
/// Provides a view over an actor's current status
/// </summary>
/// <param name="Status">The status for the account.</param>
/// <param name="Record">A record associated with the <paramref name="Status" /></param>
/// <param name="Embed">An <see cref="EmbeddedView"/> associated with the <paramref name="Status" />, if any.</param>
/// <param name="Labels">An collection of <see cref="Label"/> associated with the <paramref name="Status" />, if any.</param>
/// <param name="ExpiresAt">An <see cref="DateTimeOffset" /> when this <paramref name="Status" /> will expire, if any</param>
/// <param name="IsActive">Gets a flag indicating if the <paramref name="Status" /> is not expired. Only present <paramref name="ExpiresAt"/> was set.</param>
/// <param name="uri">The <see cref="AtUri"/> of the <paramref name="Status" />, if any.</param>
/// <param name="cid">The <see cref="Cid" /> of the <paramref name="Status" />, if any.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Documentation", "CSENSE020:Potential ghost parameter reference in documentation", Justification = "Not a ghost reference in summary.")]
public sealed record StatusView(string Status, JsonNode Record, EmbeddedView? Embed, ICollection<Label>? Labels, DateTimeOffset? ExpiresAt, bool? IsActive, AtUri? uri, Cid? cid)
{
}
