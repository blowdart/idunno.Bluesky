// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Issuance;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Retains an immutable declaration and separate permissions for new label issuance.</summary>
/// <remarks><para>The declaration includes custom definitions and extension data. Report permissions
/// are read from the declaration; accepting a report never grants permission to issue a label.</para></remarks>
public sealed class LabelPolicyDefinition
{
    /// <summary>Initializes a new instance of the <see cref="LabelPolicyDefinition"/> class.</summary>
    /// <param name="version">The unique operator-assigned policy version.</param>
    /// <param name="declaration">The complete intended labeler declaration, including its original creation time.</param>
    /// <param name="allowAccounts"><see langword="true"/> to permit account labels; otherwise, <see langword="false"/>.</param>
    /// <param name="allowRecords"><see langword="true"/> to permit record labels; otherwise, <see langword="false"/>.</param>
    /// <param name="rules">The explanatory moderation rules retained with this version.</param>
    /// <param name="allowedRecordCollections">The independent optional issuance-collection restriction.</param>
    /// <exception cref="ArgumentException">The declaration is not a bounded labeler declaration with explicit supported subjects.</exception>
    /// <exception cref="ArgumentNullException">A required value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A text or collection exceeds its storage limit.</exception>
    public LabelPolicyDefinition(
        string version,
        JsonElement declaration,
        bool allowAccounts,
        bool allowRecords,
        string rules,
        IReadOnlyCollection<Nsid>? allowedRecordCollections = null)
    {
        Rules = LabelInput.ValidateText(rules, nameof(rules), 4096);
        DeclarationJson = CanonicalDeclaration(declaration);
        if (!declaration.TryGetProperty("$type", out JsonElement type) ||
            type.ValueKind != JsonValueKind.String || type.GetString() != "app.bsky.labeler.service" ||
            !declaration.TryGetProperty("createdAt", out JsonElement created) ||
            created.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(created.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
            !declaration.TryGetProperty("policies", out JsonElement policies) || policies.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("A complete labeler service declaration is required.", nameof(declaration));
        }

        string[] subjects = ReadStrings(declaration, "subjectTypes", required: true)!;
        string[]? reasons = ReadStrings(declaration, "reasonTypes", required: false);
        string[]? collections = ReadStrings(declaration, "subjectCollections", required: false);
        ReportScope = new(subjects, reasons, collections?.Select(value => new Nsid(value)).ToArray());
        string[] values = ReadStrings(policies, "labelValues", required: true)!;
        ValidateDefinitions(policies, values);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(values.Length, 1000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(allowedRecordCollections?.Count ?? 0, 1000);
        Issuance = new(
            version: version,
            declaredValues: values,
            allowAccounts: allowAccounts,
            allowRecords: allowRecords,
            allowedRecordCollections: allowedRecordCollections);
    }

    /// <summary>Gets the complete declaration with deterministic object-property ordering.</summary>
    /// <value>The bounded JSON, including preserved extension data.</value>
    public string DeclarationJson { get; }

    /// <summary>Gets the report permissions declared by this version.</summary>
    /// <value>The immutable account and record report scope.</value>
    public LabelReportScope ReportScope { get; }

    /// <summary>Gets the independent label issuance permissions.</summary>
    /// <value>The immutable versioned issuance policy.</value>
    public LabelIssuancePolicy Issuance { get; }

    /// <summary>Gets the explanatory rules retained with this version.</summary>
    /// <value>The nonblank policy rules.</value>
    public string Rules { get; }

    /// <summary>Normalizes a bounded declaration without losing unknown properties or accepting duplicate names.</summary>
    internal static string CanonicalDeclaration(JsonElement declaration)
    {
        if (declaration.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The declaration must be an object.", nameof(declaration));
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(Encoding.UTF8.GetByteCount(declaration.GetRawText()), 65536);
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            try
            {
                WriteCanonical(writer, declaration, 0);
            }
            catch (InvalidOperationException exception)
            {
                throw new ArgumentException("The declaration contains invalid Unicode.", nameof(declaration), exception);
            }
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(buffer.WrittenCount, 65536);
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Applies shared SDK publication validation without discarding unknown definition or declaration fields.</summary>
    private static void ValidateDefinitions(JsonElement policies, IReadOnlyCollection<string> values)
    {
        List<LabelValueDefinition> definitions = [];
        if (policies.TryGetProperty("labelValueDefinitions", out JsonElement array))
        {
            if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 1000)
            {
                throw new ArgumentException("Custom definitions must be a bounded array.", nameof(policies));
            }

            try
            {
                foreach (JsonElement definition in array.EnumerateArray())
                {
                    LabelValueDefinition parsed = JsonSerializer.Deserialize(definition, LabelPolicyJsonContext.Default.LabelValueDefinition)
                        ?? throw new ArgumentException("Custom definitions cannot contain null.", nameof(policies));
                    // Required-init source generation does not preserve this optional property's initializer.
                    if (!definition.TryGetProperty("defaultSetting", out _))
                    {
                        parsed = parsed with { DefaultSetting = "warn" };
                    }

                    definitions.Add(parsed);
                }
            }
            catch (JsonException exception)
            {
                throw new ArgumentException("A custom label definition has invalid or missing fields.", nameof(policies), exception);
            }
        }

        LabelPolicyValidator.Validate(values, definitions);
    }

    /// <summary>Writes stable JSON while rejecting ambiguous objects and excessive depth.</summary>
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value, int depth)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(depth, 32);
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                if (!names.Add(property.Name))
                {
                    throw new ArgumentException("Declaration objects cannot contain duplicate property names.", nameof(value));
                }

                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value, depth + 1);
            }

            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (JsonElement item in value.EnumerateArray())
            {
                WriteCanonical(writer, item, depth + 1);
            }

            writer.WriteEndArray();
        }
        else if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetInt64(out long number))
            {
                throw new ArgumentException("Declaration numbers must be signed 64-bit integers.", nameof(value));
            }

            writer.WriteNumberValue(number);
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            writer.WriteStringValue(value.GetString());
        }
        else
        {
            value.WriteTo(writer);
        }
    }

    /// <summary>Distinguishes absent restrictions from explicit empty arrays and rejects null or nonstring entries.</summary>
    private static string[]? ReadStrings(JsonElement parent, string name, bool required)
    {
        if (!parent.TryGetProperty(name, out JsonElement array))
        {
            return required ? throw new ArgumentException("The declaration is missing " + name + ".", nameof(parent)) : null;
        }

        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 1000)
        {
            throw new ArgumentException("Declaration lists must be bounded arrays.", nameof(parent));
        }

        List<string> values = [];
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Declaration lists must contain strings.", nameof(parent));
            }

            values.Add(item.GetString()!);
        }

        return [.. values];
    }
}
