// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Labels;

/// <summary>
/// Preserves the original strings and optional field presence used to sign a label.
/// </summary>
internal sealed class LabelConverter : JsonConverter<Label>
{
    private static readonly string[] s_fields = ["ver", "src", "uri", "cid", "val", "neg", "cts", "exp", "sig"];

    /// <inheritdoc/>
    public override Label Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A label must be an object.");
        }

        StringComparer comparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, JsonElement> fields = new(comparer);
        foreach (JsonProperty property in document.RootElement.EnumerateObject().Where(property => s_fields.Contains(property.Name, comparer)))
        {
            if (fields.ContainsKey(property.Name))
            {
                throw new JsonException($"The label has a duplicate '{property.Name}' property.");
            }

            fields.Add(property.Name, property.Value);
        }

        try
        {
            int? version = null;
            if (fields.TryGetValue("ver", out JsonElement ver) && ver.ValueKind != JsonValueKind.Null)
            {
                version = ver.ValueKind == JsonValueKind.String && (options.NumberHandling & JsonNumberHandling.AllowReadingFromString) != 0
                    ? int.Parse(ver.GetString()!, CultureInfo.InvariantCulture)
                    : ver.GetInt32();
            }

            Bytes? signature = fields.TryGetValue("sig", out JsonElement sig)
                ? sig.Deserialize(SourceGenerationContext.Default.Bytes)
                : null;
            return new Label(
                version,
                new Did(RequiredString(fields, "src")),
                RequiredString(fields, "uri"),
                cid: null,
                RequiredString(fields, "val"),
                isNegationLabel: false,
                creationTimestamp: default,
                signature)
            {
                CidText = OptionalString(fields, "cid"),
                CreationTimestampText = RequiredString(fields, "cts"),
                ExpiresAtText = OptionalString(fields, "exp"),
                Negation = fields.TryGetValue("neg", out JsonElement neg)
                    ? neg.GetBoolean()
                    : null
            };
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or OverflowException or InvalidDataException)
        {
            throw new JsonException("The label is malformed.", exception);
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Label value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.Version.HasValue)
        {
            writer.WriteNumber("ver", value.Version.Value);
        }

        writer.WriteString("src", value.Source.Value);
        writer.WriteString("uri", value.Uri);
        if (value.CidText is not null)
        {
            writer.WriteString("cid", value.CidText);
        }

        writer.WriteString("val", value.Value);
        if (value.Negation.HasValue)
        {
            writer.WriteBoolean("neg", value.Negation.Value);
        }

        writer.WriteString("cts", value.CreationTimestampText);
        if (value.ExpiresAtText is not null)
        {
            writer.WriteString("exp", value.ExpiresAtText);
        }

        if (value.Signature is not null)
        {
            writer.WritePropertyName("sig");
            JsonSerializer.Serialize(writer, value.Signature, SourceGenerationContext.Default.Bytes);
        }

        writer.WriteEndObject();
    }

    private static string RequiredString(Dictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out JsonElement value)
            ? value.GetString() ?? throw new JsonException($"The label field '{name}' is null.")
            : throw new JsonException($"The required label field '{name}' is missing.");

    private static string? OptionalString(Dictionary<string, JsonElement> fields, string name) =>
        fields.ContainsKey(name) ? RequiredString(fields, name) : null;
}
