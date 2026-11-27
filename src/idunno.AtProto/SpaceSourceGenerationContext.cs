// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto.Space;
using idunno.AtProto.Space.Models;

namespace idunno.AtProto;

/// <summary>
/// Provides source-generated JSON metadata for experimental space API contracts.
/// </summary>
[JsonSourceGenerationOptions(
    AllowOutOfOrderMetadataProperties = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    IgnoreReadOnlyProperties = false,
    GenerationMode = JsonSourceGenerationMode.Default,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = false)]
[JsonSerializable(typeof(SpaceAtUri))]
[JsonSerializable(typeof(SpacePolicy))]
[JsonSerializable(typeof(PersonalSpacePolicy))]
[JsonSerializable(typeof(MemberSpacePolicy))]
[JsonSerializable(typeof(ManagedSpacePolicy))]
[JsonSerializable(typeof(UnknownSpacePolicy))]
[JsonSerializable(typeof(SpaceCommit))]
[JsonSerializable(typeof(SpaceRepoWrite))]
[JsonSerializable(typeof(SpaceRepoRemoved))]
[JsonSerializable(typeof(SpaceDescription))]
[JsonSerializable(typeof(SpaceView))]
[JsonSerializable(typeof(CreateSpaceRequest))]
[JsonSerializable(typeof(CreateSpaceResponse))]
[JsonSerializable(typeof(GetSpaceResponse))]
[JsonSerializable(typeof(ListSpacesResponse))]
[JsonSerializable(typeof(UpdateSpaceRequest))]
[JsonSerializable(typeof(DeleteSpaceRequest))]
[JsonSerializable(typeof(SpaceRecordRequest))]
[JsonSerializable(typeof(DeleteSpaceRecordRequest))]
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal partial class SpaceSourceGenerationContext : JsonSerializerContext
{
}
