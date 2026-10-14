// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto.Moderation;
using idunno.AtProto.Repo;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class JsonSerializerOptionsTests
{
    // A strong reference whose $type discriminator trails the rest of the payload, which is how an
    // AT Protocol server is free to order it.
    private const string SubjectWithTrailingTypeDiscriminator =
        """
        {"uri":"at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3kzmmwnqk2s2b","cid":"bafyreid27zk7lbis4zw5fz4podbvbs4fc5ivwji3dmrwa6zggnj4bnd57u","$type":"com.atproto.repo.strongRef"}
        """;

    public static TheoryData<string> OptionsSurfaces => new()
    {
        nameof(AtProtoJsonSerializerOptions.Options),
        nameof(AtProtoServer.AtProtoJsonSerializerOptions),
        nameof(AtProtoServer.DefaultJsonSerializerOptionsWithNoTypeResolution),
    };

    [Theory]
    [MemberData(nameof(OptionsSurfaces))]
    public void OptionsSurfaceAllowsOutOfOrderMetadataProperties(string surface)
    {
        JsonSerializerOptions options = surface switch
        {
            nameof(AtProtoJsonSerializerOptions.Options) => AtProtoJsonSerializerOptions.Options,
            nameof(AtProtoServer.AtProtoJsonSerializerOptions) => AtProtoServer.AtProtoJsonSerializerOptions,
            nameof(AtProtoServer.DefaultJsonSerializerOptionsWithNoTypeResolution) => AtProtoServer.DefaultJsonSerializerOptionsWithNoTypeResolution,
            _ => throw new InvalidOperationException($"Unknown surface {surface}."),
        };

        Assert.True(options.AllowOutOfOrderMetadataProperties);
    }

    [Fact]
    public void EveryJsonSerializerOptionsSurfaceInTheAssemblyAllowsOutOfOrderMetadataProperties()
    {
        List<string> offenders = [];

        foreach (Type type in typeof(AtProtoServer).Assembly.GetTypes())
        {
            IEnumerable<PropertyInfo> properties = type
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(property => property.PropertyType == typeof(JsonSerializerOptions) && property.GetIndexParameters().Length == 0 && property.CanRead);

            foreach (PropertyInfo property in properties)
            {
                if (property.GetValue(null) is JsonSerializerOptions options && !options.AllowOutOfOrderMetadataProperties)
                {
                    offenders.Add($"{type.FullName}.{property.Name}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void AtProtoJsonSerializerOptionsReadsAPolymorphicTypeWhoseDiscriminatorComesLast()
    {
        SubjectType? subject = JsonSerializer.Deserialize<SubjectType>(SubjectWithTrailingTypeDiscriminator, AtProtoJsonSerializerOptions.Options);

        StrongReference strongReference = Assert.IsType<StrongReference>(subject);
        Assert.Equal("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3kzmmwnqk2s2b", strongReference.Uri.ToString());
    }

    [Fact]
    public void AtProtoServerJsonSerializerOptionsReadsAPolymorphicTypeWhoseDiscriminatorComesLast()
    {
        SubjectType? subject = JsonSerializer.Deserialize<SubjectType>(SubjectWithTrailingTypeDiscriminator, AtProtoServer.AtProtoJsonSerializerOptions);

        Assert.IsType<StrongReference>(subject);
    }

    [Fact]
    public void ChainedJsonSerializerOptionsAllowOutOfOrderMetadataPropertiesAndReadAPolymorphicType()
    {
        JsonSerializerOptions chained = AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(ChainedTestContext.Default);

        Assert.True(chained.AllowOutOfOrderMetadataProperties);

        SubjectType? subject = JsonSerializer.Deserialize<SubjectType>(SubjectWithTrailingTypeDiscriminator, chained);

        Assert.IsType<StrongReference>(subject);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TolerantPolymorphicConfigurationRewrapsAnOptionsBoundResolverChain(bool configureTwice)
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            AllowOutOfOrderMetadataProperties = true
        };
        options.TypeInfoResolverChain.Add(ChainedTestContext.Default);
        options.TypeInfoResolverChain.Add(SourceGenerationContext.Default);

        Json.TolerantPolymorphicConverter.Configure(options);
        if (configureTwice)
        {
            Json.TolerantPolymorphicConverter.Configure(options);
        }
        options.MakeReadOnly();

        ChainedTestRecord record = Assert.IsType<ChainedTestRecord>(
            JsonSerializer.Deserialize<ChainedTestRecord>("""{"value":"snapshot"}""", options));
        Assert.Equal("snapshot", record.Value);
        Assert.IsType<StrongReference>(
            JsonSerializer.Deserialize<SubjectType>(SubjectWithTrailingTypeDiscriminator, options));
    }

    [Fact]
    public void JsonOptionsResolvesPolymorphicTypesFromResolversAddedAfterConstruction()
    {
        JsonOptions jsonOptions = new();
        jsonOptions.JsonSerializerOptions.TypeInfoResolverChain.Add(TolerantTestContext.Default);

        AssertLateBoundRecord(jsonOptions.JsonSerializerOptions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MutableOptionsResolveInsertedAndReplacementResolversWithoutChangingDefaults(bool replace)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Options;
        if (replace)
        {
            options.TypeInfoResolverChain.Add(ChainedTestContext.Default);
            options.TypeInfoResolverChain[1] = TolerantTestContext.Default;
        }
        else
        {
            options.TypeInfoResolverChain.Add(TolerantTestContext.Default);
        }

        AssertLateBoundRecord(options);
        Assert.Throws<NotSupportedException>(() => AtProtoJsonSerializerOptions.Default.GetTypeInfo(typeof(LateBoundRecordBase)));
        Assert.IsType<StrongReference>(JsonSerializer.Deserialize<SubjectType>(
            SubjectWithTrailingTypeDiscriminator, AtProtoJsonSerializerOptions.Default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacingOrPrependingTheSdkResolverRequiresExplicitContractChaining(bool replace)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Options;
        if (replace)
        {
            options.TypeInfoResolver = TolerantTestContext.Default;
        }
        else
        {
            options.TypeInfoResolverChain.Insert(0, TolerantTestContext.Default);
        }

        AssertLateBoundRecord(AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(options));
    }

    [Fact]
    public void CallerCopiedOptionsSupportExistingContractsAndExplicitlyChainedCustomResolvers()
    {
        JsonSerializerOptions copy = new(AtProtoJsonSerializerOptions.Default);
        Assert.IsType<StrongReference>(JsonSerializer.Deserialize<SubjectType>(
            SubjectWithTrailingTypeDiscriminator.Replace("\"uri\"", "\"$via\":\"copy\",\"uri\"", StringComparison.Ordinal), copy));

        copy = new(AtProtoJsonSerializerOptions.Default);
        copy.TypeInfoResolverChain.Add(TolerantTestContext.Default);
        AssertLateBoundRecord(AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(copy));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PropertyConvertersForwardSdkOptionsAndPreserveTheirWireFormats(bool normalizeOuter)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Options;
        options.TypeInfoResolverChain.Add(TolerantTestContext.Default);
        string json = $$$$"""
            {"$type":"test.properties",{{{{(normalizeOuter ? "\"$via\":\"outer\"," : "")}}}}
             "reference":{"record":{"$type":"test.nested","$via":"reference","value":"nested"}},
             "structure":{"record":{"$type":"test.nested","$via":"struct","value":"nested"}}}
            """;

        PropertyRecord record = Assert.IsType<PropertyRecord>(JsonSerializer.Deserialize<PropertyRecordBase>(json, options));
        Assert.Equal("reference", Assert.IsType<NestedCustomRecord>(record.Reference).ExtensionData!["$via"].GetString());
        Assert.Equal("struct", Assert.IsType<NestedCustomRecord>(record.Structure.Record).ExtensionData!["$via"].GetString());

        string serialized = JsonSerializer.Serialize<PropertyRecordBase>(record, options);
        using JsonDocument document = JsonDocument.Parse(serialized);
        Assert.Equal("reference", document.RootElement.GetProperty("reference").GetProperty("record").GetProperty("$via").GetString());
        Assert.Equal("struct", document.RootElement.GetProperty("structure").GetProperty("record").GetProperty("$via").GetString());
        Assert.IsType<PropertyRecord>(JsonSerializer.Deserialize<PropertyRecordBase>(serialized, options));
    }

    [Theory]
    [InlineData("""{"$type":"test.properties","reference":null,"structure":{"record":null}}""")]
    [InlineData("""{"$type":"test.properties","$via":"outer","reference":null,"structure":{"record":null}}""")]
    public void PropertyConvertersPreserveNullHandling(string json)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Options;
        options.TypeInfoResolverChain.Add(TolerantTestContext.Default);
        PropertyRecord record = Assert.IsType<PropertyRecord>(JsonSerializer.Deserialize<PropertyRecordBase>(json, options));
        Assert.Null(record.Reference);
        Assert.Null(record.Structure.Record);
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize<PropertyRecordBase>(record, options));
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("reference").ValueKind);
    }

    [Fact]
    public void PropertyConvertersPreserveExplicitNullConversionWhenDefaultsIgnoreNulls()
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Options;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.TypeInfoResolverChain.Add(TolerantTestContext.Default);
        PropertyRecord record = Assert.IsType<PropertyRecord>(JsonSerializer.Deserialize<PropertyRecordBase>(
            """{"$type":"test.properties","nullableText":null}""", options));
        Assert.Equal("read-null", record.NullableText);

        record.NullableText = null;
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize<PropertyRecordBase>(record, options));
        Assert.Equal("write-null", document.RootElement.GetProperty("nullableText").GetString());
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("structure")]
    public void PropertyConvertersPreserveNestedDiscriminatorValidation(string property)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Options;
        options.TypeInfoResolverChain.Add(TolerantTestContext.Default);
        string json = $$$$"""{"$type":"test.properties","{{{{property}}}}":{"record":{"$type":"invalid","$via":"extension"}}}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PropertyRecordBase>(json, options));
    }

    [Theory]
    [InlineData("""{"$type":"test.properties","reference":{"record":{"$type":"test.nested","$via":"extension"}}}""", typeof(JsonException))]
    [InlineData("""{"$type":"test.properties","structure":{"record":{"$type":"test.nested","$via":"extension"}}}""", typeof(JsonException))]
    [InlineData("""{"$type":"test.properties","structure":null}""", typeof(JsonException))]
    [InlineData("""{"$type":"test.properties","reference":{}}""", typeof(KeyNotFoundException))]
    public void PropertyConvertersPreserveRequiredPropertiesValueTypeNullsAndConverterValidation(string json, Type exceptionType)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Options;
        options.TypeInfoResolverChain.Add(TolerantTestContext.Default);
        Assert.Throws(exceptionType, () => JsonSerializer.Deserialize<PropertyRecordBase>(json, options));
    }

    [Fact]
    public void BuilderJsonOptionsResolvesPolymorphicTypesAddedDuringConfiguration()
    {
        using AtProtoAgent agent = AtProtoAgent.CreateBuilder()
            .ForService(new Uri("https://test.invalid"))
            .ConfigureHttpJsonOptions(options =>
                options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, TolerantTestContext.Default))
            .Build();

        AssertLateBoundRecord(agent.JsonOptions.JsonSerializerOptions);
    }

    [Fact]
    public void CustomStructConverterForwardsNestedPolymorphicValuesToSdkOptions()
    {
        JsonOptions jsonOptions = new();
        jsonOptions.JsonSerializerOptions.TypeInfoResolverChain.Add(TolerantTestContext.Default);

        const string json = """
            {"$type":"test.customContainer","payload":{"record":{"$type":"test.nested","$via":"extension","value":"nested"}}}
            """;

        RecordWithCustomStruct? record = JsonSerializer.Deserialize<CustomStructRecordBase>(json, jsonOptions.JsonSerializerOptions) as RecordWithCustomStruct;

        Assert.NotNull(record);
        NestedCustomRecord nested = Assert.IsType<NestedCustomRecord>(record.Payload.Record);
        Assert.Equal("nested", nested.Value);
        Assert.Equal("extension", nested.ExtensionData!["$via"].GetString());
    }

    public static TheoryData<string> SharedOptionsSurfaces => new()
    {
        nameof(AtProtoJsonSerializerOptions.Default),
        nameof(AtProtoServer.AtProtoJsonSerializerOptions),
    };

    [Theory]
    [MemberData(nameof(SharedOptionsSurfaces))]
    public void SharedOptionsAreTheSameReadOnlyInstanceOnEveryCall(string surface)
    {
        Func<JsonSerializerOptions> get = surface switch
        {
            nameof(AtProtoJsonSerializerOptions.Default) => () => AtProtoJsonSerializerOptions.Default,
            nameof(AtProtoServer.AtProtoJsonSerializerOptions) => () => AtProtoServer.AtProtoJsonSerializerOptions,
            _ => throw new InvalidOperationException($"Unknown surface {surface}."),
        };

        JsonSerializerOptions options = get();

        Assert.Same(options, get());
        Assert.True(options.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => options.Converters.Add(new JsonStringEnumConverter()));
    }

    [Fact]
    public void DefaultJsonSerializerOptionsWithNoTypeResolutionIsTheSameInstanceOnEveryCall()
    {
        Assert.Same(AtProtoServer.DefaultJsonSerializerOptionsWithNoTypeResolution, AtProtoServer.DefaultJsonSerializerOptionsWithNoTypeResolution);
    }

    [Fact]
    public void OptionsReturnsANewMutableInstanceOnEveryCall()
    {
        JsonSerializerOptions first = AtProtoJsonSerializerOptions.Options;
        int converterCount = AtProtoJsonSerializerOptions.Default.Converters.Count;

        Assert.NotSame(first, AtProtoJsonSerializerOptions.Options);
        Assert.NotSame(AtProtoJsonSerializerOptions.Default, first);
        Assert.False(first.IsReadOnly);

        first.Converters.Add(new JsonStringEnumConverter());

        Assert.Equal(converterCount, AtProtoJsonSerializerOptions.Default.Converters.Count);
    }

    [Fact]
    public void TypeInfoResolverIsTheDefaultResolver()
    {
        Assert.Same(AtProtoJsonSerializerOptions.Default.TypeInfoResolver, AtProtoJsonSerializerOptions.TypeInfoResolver);
    }

    [Fact]
    public void ChainedJsonSerializerOptionsAreNewMutableInstancesWhichDoNotChangeTheSharedOptions()
    {
        int sharedResolverCount = AtProtoServer.AtProtoJsonSerializerOptions.TypeInfoResolverChain.Count;

        JsonSerializerOptions first = AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(ChainedTestContext.Default);
        JsonSerializerOptions second = AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(ChainedTestContext.Default);

        Assert.NotSame(first, second);
        Assert.NotSame(AtProtoServer.AtProtoJsonSerializerOptions, first);
        Assert.False(first.IsReadOnly);
        Assert.Equal(sharedResolverCount, AtProtoServer.AtProtoJsonSerializerOptions.TypeInfoResolverChain.Count);
        Assert.Equal(typeof(ChainedTestRecord), first.GetTypeInfo(typeof(ChainedTestRecord)).Type);
        Assert.Throws<NotSupportedException>(() => AtProtoServer.AtProtoJsonSerializerOptions.GetTypeInfo(typeof(ChainedTestRecord)));
    }

    private static void AssertLateBoundRecord(JsonSerializerOptions options)
    {
        const string json = """{"$type":"test.lateBound","$via":"extension","value":"late"}""";
        LateBoundRecordBase? record = JsonSerializer.Deserialize<LateBoundRecordBase>(json, options);

        LateBoundRecord lateBound = Assert.IsType<LateBoundRecord>(record);
        Assert.Equal("late", lateBound.Value);
        Assert.Equal("extension", lateBound.ExtensionData!["$via"].GetString());
    }
}

/// <summary>
/// A second, unrelated resolver so the chaining helper has something to chain that is not already
/// the resolver it builds on top of.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ChainedTestRecord))]
[ExcludeFromCodeCoverage]
internal sealed partial class ChainedTestContext : JsonSerializerContext
{
}

[ExcludeFromCodeCoverage]
internal sealed record ChainedTestRecord(string Value);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(LateBoundRecord), "test.lateBound")]
internal abstract class LateBoundRecordBase;

internal sealed class LateBoundRecord : LateBoundRecordBase
{
    public string Value { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(RecordWithCustomStruct), "test.customContainer")]
internal abstract record CustomStructRecordBase;

internal sealed record RecordWithCustomStruct(CustomStructPayload Payload) : CustomStructRecordBase;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(NestedCustomRecord), "test.nested")]
internal abstract class NestedCustomRecordBase;

internal sealed class NestedCustomRecord : NestedCustomRecordBase
{
    [JsonRequired]
    public string Value { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

[JsonConverter(typeof(CustomStructPayloadConverter))]
internal readonly record struct CustomStructPayload(NestedCustomRecordBase? Record);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(PropertyRecord), "test.properties")]
internal abstract class PropertyRecordBase;

internal sealed class PropertyRecord : PropertyRecordBase
{
    [JsonConverter(typeof(PropertyReferenceConverter))]
    public NestedCustomRecordBase? Reference { get; set; }

    [JsonConverter(typeof(PropertyStructConverterFactory))]
    public PropertyStructPayload Structure { get; set; }

    [JsonConverter(typeof(NullAwareTextConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? NullableText { get; set; }
}

internal readonly record struct PropertyStructPayload(NestedCustomRecordBase? Record);

internal sealed class PropertyStructConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(PropertyStructPayload);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) => new PropertyStructConverter();
}

internal sealed class NullAwareTextConverter : JsonConverter<string>
{
    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? "read-null" : reader.GetString();

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value ?? "write-null");
}

internal sealed class PropertyReferenceConverter : JsonConverter<NestedCustomRecordBase>
{
    public override NestedCustomRecordBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        return JsonSerializer.Deserialize<NestedCustomRecordBase>(document.RootElement.GetProperty("record"), options);
    }

    public override void Write(Utf8JsonWriter writer, NestedCustomRecordBase value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("record");
        JsonSerializer.Serialize(writer, value, options);
        writer.WriteEndObject();
    }
}

internal sealed class PropertyStructConverter : JsonConverter<PropertyStructPayload>
{
    public override PropertyStructPayload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        return new(JsonSerializer.Deserialize<NestedCustomRecordBase>(document.RootElement.GetProperty("record"), options));
    }

    public override void Write(Utf8JsonWriter writer, PropertyStructPayload value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("record");
        JsonSerializer.Serialize(writer, value.Record, options);
        writer.WriteEndObject();
    }
}

internal sealed class CustomStructPayloadConverter : JsonConverter<CustomStructPayload>
{
    public override CustomStructPayload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        NestedCustomRecordBase? record = JsonSerializer.Deserialize<NestedCustomRecordBase>(document.RootElement.GetProperty("record"), options);
        return new(record);
    }

    public override void Write(Utf8JsonWriter writer, CustomStructPayload value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("record");
        JsonSerializer.Serialize(writer, value.Record, options);
        writer.WriteEndObject();
    }
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(LateBoundRecordBase))]
[JsonSerializable(typeof(LateBoundRecord))]
[JsonSerializable(typeof(CustomStructRecordBase))]
[JsonSerializable(typeof(RecordWithCustomStruct))]
[JsonSerializable(typeof(CustomStructPayload))]
[JsonSerializable(typeof(NestedCustomRecordBase))]
[JsonSerializable(typeof(NestedCustomRecord))]
[JsonSerializable(typeof(PropertyRecordBase))]
[JsonSerializable(typeof(PropertyRecord))]
[JsonSerializable(typeof(PropertyStructPayload))]
[ExcludeFromCodeCoverage]
internal sealed partial class TolerantTestContext : JsonSerializerContext
{
}
