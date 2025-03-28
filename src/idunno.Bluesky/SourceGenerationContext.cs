// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Actions;
using idunno.Bluesky.Record;

namespace idunno.Bluesky
{
    /// <exclude />
    [JsonSourceGenerationOptions(
        AllowOutOfOrderMetadataProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyProperties = false,
        GenerationMode = JsonSourceGenerationMode.Metadata,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        WriteIndented = false,
        UseStringEnumConverter = true)]

    [JsonSerializable(typeof(BlockRecordValue))]
    [JsonSerializable(typeof(FollowRecordValue))]
    [JsonSerializable(typeof(LikeRecordValue))]
    [JsonSerializable(typeof(RepostRecordValue))]

    [JsonSerializable(typeof(Post))]
    [JsonSerializable(typeof(PostRecord))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
