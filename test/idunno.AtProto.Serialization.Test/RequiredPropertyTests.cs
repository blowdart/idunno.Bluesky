// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto.Labels;

namespace idunno.AtProto.Serialization.Test;

[ExcludeFromCodeCoverage]
public class RequiredPropertyTests
{
    [Fact]
    public void LabelWithNoSignatureDeserializesWithANullSignature()
    {
        string json = """
            {
                "src": "did:plc:abc123abc123abc123abc123",
                "uri": "at://did:plc:abc123abc123abc123abc123/app.bsky.feed.post/3k",
                "val": "spam",
                "cts": "2024-01-01T00:00:00Z"
            }
            """;

        Label? actual = JsonSerializer.Deserialize<Label>(json, options: AtProtoServer.AtProtoJsonSerializerOptions);

        Assert.NotNull(actual);
        Assert.Null(actual.Signature);
    }

    [Fact]
    public void LabelWithASignatureDeserializesItFromItsBytesEncoding()
    {
        // Over JSON the AT Protocol data model encodes bytes as a $bytes object, so a signed label, which is what a
        // labeler returns from com.atproto.label.queryLabels, has to round trip through that rather than a byte array.
        const string signature = "Pw9eKRQI9I2k+wqI0ZS4CHx8WKblSYOODNq7SI2Rto9fhjDcBXLaCJbn1t9SeIIaNaT4hmmKl41EcyyzyBzh4g";

        string json = $$"""
            {
                "ver": 1,
                "src": "did:plc:abc123abc123abc123abc123",
                "uri": "at://did:plc:abc123abc123abc123abc123/app.bsky.feed.post/3k",
                "val": "spam",
                "cts": "2024-01-01T00:00:00Z",
                "sig": { "$bytes": "{{signature}}" }
            }
            """;

        Label? actual = JsonSerializer.Deserialize<Label>(json, options: AtProtoServer.AtProtoJsonSerializerOptions);

        Assert.NotNull(actual);
        Assert.NotNull(actual.Signature);
        Assert.Equal(64, actual.Signature.Value.Count);

        string serialized = JsonSerializer.Serialize(actual, options: AtProtoServer.AtProtoJsonSerializerOptions);

        using JsonDocument document = JsonDocument.Parse(serialized);

        // The signature is unpadded in the response, and is normalized to its padded form when it is written back out.
        Assert.Equal(
            $"{signature}==",
            document.RootElement.GetProperty("sig").GetProperty("$bytes").GetString());
    }

    [Fact]
    public void LabelSignatureIsAnnotatedAsNullable()
    {
        // The sig property is optional in com.atproto.label.defs, so the annotation, not just the runtime value,
        // has to say so. A runtime null check alone would pass whether or not the member is declared nullable.
        PropertyInfo property = typeof(Label).GetProperty(nameof(Label.Signature))!;
        NullabilityInfo nullability = new NullabilityInfoContext().Create(property);

        Assert.Equal(NullabilityState.Nullable, nullability.ReadState);
    }

    [Fact]
    public void NoFieldInTheAtProtoAssemblyCarriesJsonRequired()
    {
        Assert.Empty(FieldsCarryingJsonRequired(typeof(Label).Assembly));
    }

    /// <summary>
    /// <see cref="JsonRequiredAttribute"/> applied with the <c>field:</c> target on a positional record parameter
    /// lands on the compiler generated backing field, where <see cref="JsonSerializer"/> ignores it, so the property
    /// is silently not required. Only the <c>property:</c> target has any effect.
    /// </summary>
    internal static List<string> FieldsCarryingJsonRequired(Assembly assembly)
    {
        List<string> offenders = [];

        foreach (Type type in assembly.GetTypes())
        {
            FieldInfo[] fields = type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in fields)
            {
                if (field.GetCustomAttribute<JsonRequiredAttribute>() is not null)
                {
                    offenders.Add($"{type.FullName}.{field.Name}");
                }
            }
        }

        return offenders;
    }
}
