// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo;

/// <summary>
/// Identifies a blob referenced by a repository record that is missing from the repository.
/// </summary>
/// <param name="Cid">The content identifier of the missing blob.</param>
/// <param name="RecordUri">The AT URI of the record that references the blob.</param>
public sealed record MissingBlob(
    [property: JsonRequired] Cid Cid,
    [property: JsonRequired] AtUri RecordUri) : AtProtoObject;
