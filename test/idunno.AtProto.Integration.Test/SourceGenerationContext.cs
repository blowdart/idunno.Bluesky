// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

namespace idunno.AtProto.Integration.Test
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

    [JsonSerializable(typeof(TestRecord))]
    [JsonSerializable(typeof(TestRecordValue))]
    [JsonSerializable(typeof(AtProtoRecord<TestRecordValue>))]
    [JsonSerializable(typeof(DidDocument))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
