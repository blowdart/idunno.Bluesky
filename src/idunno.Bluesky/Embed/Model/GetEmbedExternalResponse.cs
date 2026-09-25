// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Embed.Model;

/// <summary>
/// The response from app.bsky.embed.getEmbedExternalView.
/// </summary>
/// <param name="View">
/// The hydrated view of the embed, or <see langword="null" /> when no records resolved or validation failed.
/// </param>
/// <remarks>
/// <para><see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/embed/getEmbedExternalView.json">app.bsky.embed.getEmbedExternalView</see>
/// returns an empty object when no records were resolvable, or when the resolved records do not back the requested URL, so every property is optional.</para>
/// </remarks>
internal sealed record GetEmbedExternalResponse(EmbeddedExternalView? View)
{
}