// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.AtProto.Sync.Model;
#pragma warning restore IDE0130 // Namespace does not match folder structure

internal sealed record ListReposByCollectionResponse([property: JsonRequired] List<ListReposByCollectionRepo> Repos, string? Cursor);

internal sealed record ListReposByCollectionRepo([property: JsonRequired] Did Did);
