// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Labels;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Provides trimming-safe custom-definition parsing without making remote DTO deserialization stricter globally.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(LabelValueDefinition))]
internal sealed partial class LabelPolicyJsonContext : JsonSerializerContext;
