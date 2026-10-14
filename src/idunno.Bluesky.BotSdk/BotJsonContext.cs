// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto.Jetstream.Archive;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.BotSdk;

// Native AOT cannot discover arbitrary record properties at runtime. Generate the metadata
// for our credentials and probe responses at build time; SDK types use the SDK's own metadata.
/// <summary>Generates AOT-safe metadata for application-owned payloads, credential envelopes, and health responses.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SavedCredentials))]
[JsonSerializable(typeof(LivenessResponse))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(BookmarkIntent))]
[JsonSerializable(typeof(ListItemIntent))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class BotJsonContext : JsonSerializerContext;

/// <summary>Reuses the SDK's generated record and replay contracts instead of discovering types through reflection.</summary>
internal static class BotJson
{
    // The SDK already generates metadata for its types, including converters that are internal
    // to its assembly. Reuse that metadata rather than trying to regenerate it in this application.
    // Keep the original default naming/null policy for database state so existing JSON still reads.
    private static readonly JsonSerializerOptions s_stateOptions = new()
    {
        TypeInfoResolver = BlueskyJsonSerializerOptions.TypeInfoResolver
    };

    public static JsonTypeInfo<SnapshotRequest> SnapshotRequest { get; } =
        (JsonTypeInfo<SnapshotRequest>)s_stateOptions.GetTypeInfo(typeof(SnapshotRequest));

    public static JsonTypeInfo<SnapshotCheckpoint> SnapshotCheckpoint { get; } =
        (JsonTypeInfo<SnapshotCheckpoint>)s_stateOptions.GetTypeInfo(typeof(SnapshotCheckpoint));

    public static JsonTypeInfo<Post> Post { get; } =
        (JsonTypeInfo<Post>)BlueskyJsonSerializerOptions.Default.GetTypeInfo(typeof(Post));

    public static JsonTypeInfo<Like> Like { get; } =
        (JsonTypeInfo<Like>)BlueskyJsonSerializerOptions.Default.GetTypeInfo(typeof(Like));

    public static JsonTypeInfo<Repost> Repost { get; } =
        (JsonTypeInfo<Repost>)BlueskyJsonSerializerOptions.Default.GetTypeInfo(typeof(Repost));

    public static JsonTypeInfo<BlueskyRecord> BlueskyRecord { get; } =
        (JsonTypeInfo<BlueskyRecord>)BlueskyJsonSerializerOptions.Default.GetTypeInfo(typeof(BlueskyRecord));

}

// Keep the original credential envelope's property names readable across upgrades.
/// <summary>Stores an encrypted refresh-session payload using the original compatible property names.</summary>
/// <param name="Service">The authentication service associated with the refresh token.</param>
/// <param name="RefreshToken">The secret refresh token, which must never be logged.</param>
internal sealed record SavedCredentials(
    [property: JsonPropertyName("Service")] string Service,
    [property: JsonPropertyName("RefreshToken")] string RefreshToken)
{
    public override string ToString() => "[REDACTED]";
}

/// <summary>Preserves the public liveness response's status and per-worker diagnostic shape.</summary>
/// <param name="Status">The overall healthy or unhealthy status.</param>
/// <param name="Workers">The detached in-memory worker states.</param>
internal sealed record LivenessResponse(
    string Status,
    Dictionary<string, HealthState> Workers);
