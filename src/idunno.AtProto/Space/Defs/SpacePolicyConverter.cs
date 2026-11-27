// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Converts known space policies and preserves unrecognized policy objects.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal sealed class SpacePolicyConverter : JsonConverter<SpacePolicy>
{
    /// <inheritdoc/>
    public override SpacePolicy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement value = document.RootElement;
        if (value.ValueKind is not JsonValueKind.Object ||
            !value.TryGetProperty("$type", out JsonElement discriminator) ||
            discriminator.ValueKind is not JsonValueKind.String ||
            string.IsNullOrWhiteSpace(discriminator.GetString()))
        {
            throw new JsonException("A space policy must be an object with a nonempty string $type.");
        }

        return discriminator.GetString() switch
        {
            "com.atproto.personalspace.policy" => value.Deserialize((JsonTypeInfo<PersonalSpacePolicy>)options.GetTypeInfo(typeof(PersonalSpacePolicy)))!,
            "com.atproto.memberspace.policy" => value.Deserialize((JsonTypeInfo<MemberSpacePolicy>)options.GetTypeInfo(typeof(MemberSpacePolicy)))!,
            "com.atproto.managedspace.policy" => value.Deserialize((JsonTypeInfo<ManagedSpacePolicy>)options.GetTypeInfo(typeof(ManagedSpacePolicy)))!,
            _ => new UnknownSpacePolicy(value)
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, SpacePolicy value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case PersonalSpacePolicy personal:
                JsonSerializer.Serialize(writer, personal, (JsonTypeInfo<PersonalSpacePolicy>)options.GetTypeInfo(typeof(PersonalSpacePolicy)));
                break;
            case MemberSpacePolicy member:
                JsonSerializer.Serialize(writer, member, (JsonTypeInfo<MemberSpacePolicy>)options.GetTypeInfo(typeof(MemberSpacePolicy)));
                break;
            case ManagedSpacePolicy managed:
                JsonSerializer.Serialize(writer, managed, (JsonTypeInfo<ManagedSpacePolicy>)options.GetTypeInfo(typeof(ManagedSpacePolicy)));
                break;
            case UnknownSpacePolicy unknown:
                unknown.Value.WriteTo(writer);
                break;
            default:
                throw new JsonException("The space policy type is not supported.");
        }
    }
}

/// <summary>
/// Converts an unrecognized policy directly without losing its discriminator or fields.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal sealed class UnknownSpacePolicyConverter : JsonConverter<UnknownSpacePolicy>
{
    /// <inheritdoc/>
    public override UnknownSpacePolicy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        try
        {
            return new UnknownSpacePolicy(document.RootElement);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("A space policy must be an object with a nonempty string $type.", exception);
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, UnknownSpacePolicy value, JsonSerializerOptions options) =>
        value.Value.WriteTo(writer);
}
