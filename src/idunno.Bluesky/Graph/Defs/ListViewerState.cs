// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Graph;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates the relationship between the authenticated actor and the list.
/// </summary>
/// <param name="Muted">Gets a flag indicating where the actor been mutes using the list.</param>
/// <param name="Blocked">Gets the <see cref="AtUri"/> to the record indicating the actor blocks using the list. A client can delete this record to undo the blocks.</param>
/// <param name="ReferenceListOptOut">Gets the <see cref="AtUri"/> to a record indicating the actor has opted out of appearing in the list. Only set for reference lists. A client can delete this record to undo the opt-out.</param>
public record ListViewerState(bool? Muted, AtUri? Blocked, AtUri? ReferenceListOptOut)
{
}