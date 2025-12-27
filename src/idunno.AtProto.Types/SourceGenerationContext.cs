// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto
{
    /// <exclude />
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

    [JsonSerializable(typeof(AtIdentifier))]
    [JsonSerializable(typeof(AtUri))]
    [JsonSerializable(typeof(Cid))]
    [JsonSerializable(typeof(Did))]
    [JsonSerializable(typeof(Handle))]
    [JsonSerializable(typeof(Nsid))]
    [JsonSerializable(typeof(RecordKey))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
