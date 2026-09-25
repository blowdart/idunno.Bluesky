// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Graph;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Possible purposes of a list
/// </summary>
/// <remarks>
///<para>See https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/graph/defs.json</para>
/// </remarks>
[JsonConverter(typeof(ListPurposeConverter))]
public enum ListPurpose
{
    /// <summary>
    /// The list has a purpose this library does not recognize.
    /// </summary>
    /// <remarks>
    /// <para>The lexicon definition of a list purpose is an open union, so the service may introduce purposes which
    /// postdate this library. Such a purpose is surfaced as <see cref="Unknown"/> rather than failing to deserialize.
    /// A list whose purpose is <see cref="Unknown"/> cannot be serialized.</para>
    /// </remarks>
    Unknown,

    /// <summary>
    /// The list is a moderation list.
    /// </summary>
    [JsonStringEnumMemberName("app.bsky.graph.defs#modlist")]
    ModList,

    /// <summary>
    /// The list is a curation list.
    /// </summary>
    [JsonStringEnumMemberName("app.bsky.graph.defs#curatelist")]
    CurateList,

    /// <summary>
    /// The list is a reference list, like starter pack.
    /// </summary>
    [JsonStringEnumMemberName("app.bsky.graph.defs#referencelist")]
    ReferenceList
}