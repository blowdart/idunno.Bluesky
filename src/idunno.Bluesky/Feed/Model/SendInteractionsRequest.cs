// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Feed.Model;

/// <summary>
/// The wire format of the request body for <c>app.bsky.feed.sendInteractions</c>.
/// </summary>
/// <param name="Interactions">The interactions to send.</param>
/// <param name="Feed">The optional <see cref="AtUri"/> of the feed the interactions occurred in.</param>
internal sealed record SendInteractionsRequest(ICollection<Interaction> Interactions, AtUri? Feed);
