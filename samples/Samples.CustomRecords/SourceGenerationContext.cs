// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Samples.CustomRecords
{
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

    [JsonSerializable(typeof(Track))]
    internal sealed partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
