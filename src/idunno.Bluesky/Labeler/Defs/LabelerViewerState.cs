// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Labeler;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Gets a view of the relationship between the requesting actor of a labeler view, and the labeler.
/// </summary>
public sealed record LabelerViewerState
{
    /// <summary>
    /// Gets the <see cref="AtUri"/> of the actor's like record for the labeler, if any.
    /// </summary>
    public AtUri? Like { get; init; }
}