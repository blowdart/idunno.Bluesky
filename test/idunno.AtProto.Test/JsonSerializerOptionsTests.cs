// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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

    [Fact]
    public void DecoratedResolverRetainsContractsAppendedBeforeDecoration()
    {
        JsonOptions jsonOptions = new();
        IJsonTypeInfoResolver appendedResolver = ChainedTestContext.Default.WithAddedModifier(info =>
        {
            if (info.Type == typeof(ChainedTestRecord))
            {
                JsonPropertyInfo property = Assert.Single(info.Properties);
                property.Name = "custom" + property.Name;
            }
        });
        jsonOptions.JsonSerializerOptions.TypeInfoResolverChain.Add(appendedResolver);
        jsonOptions.JsonSerializerOptions.TypeInfoResolver =
            jsonOptions.JsonSerializerOptions.TypeInfoResolver!.WithAddedModifier(info =>
            {
                if (info.Type == typeof(ChainedTestRecord))
                {
                    JsonPropertyInfo property = Assert.Single(info.Properties);
                    property.Name = "sdk" + property.Name;
                }
            });
        using AtProtoAgent agent = new(new Uri("https://test.invalid"), new AtProtoAgentOptions { HttpJsonOptions = jsonOptions });
        JsonTypeInfo recordInfo = agent.JsonOptions.JsonSerializerOptions.GetTypeInfo(typeof(ChainedTestRecord));

        Assert.Equal("sdkcustomvalue", Assert.Single(recordInfo.Properties).Name);
    }

    [Fact]
    public void FactoryFinalizationPreservesCallerCompositeResolverBehavior()
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = new CustomCompositeTypeInfoResolver(ChainedTestContext.Default));

        ChainedTestRecord record = Assert.IsType<ChainedTestRecord>(
            JsonSerializer.Deserialize<ChainedTestRecord>("""{"customValue":"factory"}""", options));

        Assert.Equal("factory", record.Value);
    }

    [Fact]
    public void AgentFinalizationPreservesCallerCompositeResolverBehavior()
    {
        JsonOptions supplied = new();
        supplied.JsonSerializerOptions.TypeInfoResolver =
            new CustomCompositeTypeInfoResolver(ChainedTestContext.Default);
        using AtProtoAgent agent = new(new Uri("https://test.invalid"), new AtProtoAgentOptions { HttpJsonOptions = supplied });

        ChainedTestRecord record = Assert.IsType<ChainedTestRecord>(
            JsonSerializer.Deserialize<ChainedTestRecord>("""{"customValue":"agent"}""", agent.JsonOptions.JsonSerializerOptions));

        Assert.Equal("agent", record.Value);
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

        JsonSerializerOptions recovered = AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(options);
        if (replace)
        {
            Assert.Throws<JsonException>(() => AssertLateBoundRecord(options));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => AssertLateBoundRecord(options));
        }
        AssertLateBoundRecord(recovered);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FactoryFinalizesAfterResolverConfigurationWithoutFreezingTheCallbackOptions(bool replace)
    {
        JsonSerializerOptions? configured = null;
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
        {
            configured = mutable;
            Assert.Empty(mutable.Converters);
            if (replace)
            {
                mutable.TypeInfoResolver = TolerantTestContext.Default;
            }
            else
            {
                mutable.TypeInfoResolverChain.Insert(0, TolerantTestContext.Default);
            }
            mutable.WriteIndented = true;
            mutable.Converters.Add(new NullAwareTextConverter());
        });

        Assert.True(options.IsReadOnly);
        Assert.True(options.WriteIndented);
        Assert.Contains(options.Converters, converter => converter is NullAwareTextConverter);
        Assert.Equal("read-null", JsonSerializer.Deserialize<string>("null", options));
        Assert.Equal("\"write-null\"", JsonSerializer.Serialize<string?>(null, options));
        Assert.NotNull(configured);
        Assert.False(configured.IsReadOnly);
        Assert.NotSame(configured, options);
        configured.TypeInfoResolver = ChainedTestContext.Default;
        AssertLateBoundRecord(options);
        Assert.Throws<InvalidOperationException>(() => options.TypeInfoResolverChain.Clear());
        Assert.Throws<NotSupportedException>(() => AtProtoJsonSerializerOptions.Default.GetTypeInfo(typeof(LateBoundRecordBase)));
    }

    [Fact]
    public void FactoryRejectsNullConfiguration()
    {
        Assert.Throws<ArgumentNullException>(() => AtProtoJsonSerializerOptions.Create(null!));
    }

    [Fact]
    public void FinalizingAnAgentDoesNotFreezeTheCallersLiveResolver()
    {
        JsonOptions supplied = new(AtProtoJsonSerializerOptions.Options);
        using AtProtoAgent agent = new(new Uri("https://test.invalid"), new AtProtoAgentOptions { HttpJsonOptions = supplied });
        supplied.JsonSerializerOptions.TypeInfoResolverChain.Add(TolerantTestContext.Default);
        AssertLateBoundRecord(supplied.JsonSerializerOptions);
        Assert.Throws<NotSupportedException>(() => agent.JsonOptions.JsonSerializerOptions.GetTypeInfo(typeof(LateBoundRecordBase)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AgentsFinalizeConstructorAndBuilderOptionsAfterAllConfiguration(bool builder, bool replace)
    {
        JsonOptions supplied = new();
        AtProtoAgentBuilder agentBuilder = AtProtoAgent.CreateBuilder()
            .ForService(new Uri("https://test.invalid"))
            .ConfigureHttpJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);
        if (builder)
        {
            supplied = Assert.IsType<JsonOptions>(agentBuilder.JsonOptions);
        }
        if (replace)
        {
            supplied.JsonSerializerOptions.TypeInfoResolver = TolerantTestContext.Default;
        }
        else
        {
            supplied.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, TolerantTestContext.Default);
        }
        var resolver = supplied.JsonSerializerOptions.TypeInfoResolver;
        int converters = supplied.JsonSerializerOptions.Converters.Count;
        using AtProtoAgent agent = builder
            ? agentBuilder.Build()
            : new(new Uri("https://test.invalid"), new AtProtoAgentOptions { HttpJsonOptions = supplied });

        Assert.True(agent.JsonOptions.JsonSerializerOptions.IsReadOnly);
        Assert.NotSame(supplied, agent.JsonOptions);
        Assert.False(supplied.JsonSerializerOptions.IsReadOnly);
        Assert.Same(resolver, supplied.JsonSerializerOptions.TypeInfoResolver);
        Assert.Equal(converters, supplied.JsonSerializerOptions.Converters.Count);
        supplied.JsonSerializerOptions.TypeInfoResolver = ChainedTestContext.Default;
        AssertLateBoundRecord(agent.JsonOptions.JsonSerializerOptions);
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
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void PropertyConvertersForwardSdkOptionsAndPreserveTheirWireFormats(bool normalizeOuter, bool finalized, bool preserveReferences)
    {
        JsonSerializerOptions options = finalized
            ? AtProtoJsonSerializerOptions.Create(mutable =>
            {
                mutable.TypeInfoResolver = TolerantTestContext.Default;
                mutable.ReferenceHandler = preserveReferences ? ReferenceHandler.Preserve : null;
            })
            : AtProtoJsonSerializerOptions.Options;
        if (!finalized)
        {
            options.TypeInfoResolverChain.Add(TolerantTestContext.Default);
        }
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

    [Fact]
    public void FinalizationPreservesMetadataCallbacksAndRequiredValidation()
    {
        int callbackCount = 0;
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = TolerantTestContext.Default.WithAddedModifier(info =>
            {
                if (info.Type == typeof(NestedCustomRecord))
                {
                    info.OnDeserialized = value =>
                    {
                        Assert.Equal("callback", ((NestedCustomRecord)value).ExtensionData!["$via"].GetString());
                        callbackCount++;
                    };
                }
            }));

        Assert.IsType<NestedCustomRecord>(JsonSerializer.Deserialize<NestedCustomRecordBase>(
            """{"$type":"test.nested","$via":"callback","value":"nested"}""", options));
        Assert.Equal(1, callbackCount);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NestedCustomRecordBase>(
            """{"$type":"test.nested","$via":"callback"}""", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NestedCustomRecordBase>(
            """{"$type":"invalid","$via":"callback","value":"nested"}""", options));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypeAttachedReferenceConvertersPreserveNullSentinelsAndDefaultNullPolicy(bool normalizeOuter)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = TolerantTestContext.Default);
        string metadata = normalizeOuter ? "\"$via\":\"outer\"," : "";
        string sentinelJson = $$"""{"$type":"test.nullSentinel",{{metadata}}"value":null}""";
        SentinelNullRecord sentinel = Assert.IsType<SentinelNullRecord>(
            JsonSerializer.Deserialize<NullPolicyRecordBase>(sentinelJson, options));
        Assert.Equal("read-null", sentinel.Value!.Value);
        sentinel.Value = null;
        using JsonDocument sentinelDocument = JsonDocument.Parse(JsonSerializer.Serialize<NullPolicyRecordBase>(sentinel, options));
        Assert.Equal("write-null", sentinelDocument.RootElement.GetProperty("value").GetString());

        string defaultJson = $$"""{"$type":"test.nullDefault",{{metadata}}"value":null}""";
        DefaultNullRecord defaultRecord = Assert.IsType<DefaultNullRecord>(
            JsonSerializer.Deserialize<NullPolicyRecordBase>(defaultJson, options));
        Assert.Null(defaultRecord.Value);
        using JsonDocument defaultDocument = JsonDocument.Parse(JsonSerializer.Serialize<NullPolicyRecordBase>(defaultRecord, options));
        Assert.Equal(JsonValueKind.Null, defaultDocument.RootElement.GetProperty("value").ValueKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypeAttachedReferenceConvertersPreserveNullRejection(bool normalizeOuter)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = TolerantTestContext.Default);
        string metadata = normalizeOuter ? "\"$via\":\"outer\"," : "";
        string json = $$"""{"$type":"test.nullRejected",{{metadata}}"value":null}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NullPolicyRecordBase>(json, options));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize<NullPolicyRecordBase>(new RejectedNullRecord(), options));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuilderJsonOptionsResolvesPolymorphicTypesAddedDuringConfiguration(bool replace)
    {
        using AtProtoAgent agent = AtProtoAgent.CreateBuilder()
            .ForService(new Uri("https://test.invalid"))
            .ConfigureHttpJsonOptions(options =>
            {
                if (replace)
                {
                    options.JsonSerializerOptions.TypeInfoResolver = TolerantTestContext.Default;
                }
                else
                {
                    options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, TolerantTestContext.Default);
                }
            })
            .Build();

        AssertLateBoundRecord(agent.JsonOptions.JsonSerializerOptions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustomStructConverterForwardsNestedPolymorphicValuesToSdkOptions(bool preserveReferences)
    {
        JsonOptions jsonOptions = new();
        jsonOptions.JsonSerializerOptions.ReferenceHandler = preserveReferences ? ReferenceHandler.Preserve : null;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DerivedCollectionsPreserveNestedPolymorphicMetadata(bool normalizeOuter)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = TolerantTestContext.Default);
        string metadata = normalizeOuter ? "\"$via\":\"outer\"," : "";
        string json = $$$$"""
            {"$type":"test.collections",{{{{metadata}}}}
             "records":[{"$type":"test.nested","$via":"list","value":"first"}],
             "dictionary":{"entry":{"$type":"test.nested","$via":"dictionary","value":"second"}},
             "interfaceRecords":[{"$type":"test.nested","$via":"interface-list","value":"third"}],
             "interfaceDictionary":{"entry":{"$type":"test.nested","$via":"interface-dictionary","value":"fourth"}}}
            """;

        foreach (string payload in new[] { json, JsonSerializer.Serialize(
            JsonSerializer.Deserialize<CollectionRecordBase>(json, options), options) })
        {
            CollectionRecord record = Assert.IsType<CollectionRecord>(
                JsonSerializer.Deserialize<CollectionRecordBase>(payload, options));
            NestedCustomRecord first = Assert.IsType<NestedCustomRecord>(Assert.Single(record.Records));
            NestedCustomRecord second = Assert.IsType<NestedCustomRecord>(record.Dictionary["entry"]);
            Assert.Equal("first", first.Value);
            Assert.Equal("list", first.ExtensionData!["$via"].GetString());
            Assert.Equal("second", second.Value);
            Assert.Equal("dictionary", second.ExtensionData!["$via"].GetString());
            NestedCustomRecord third = Assert.IsType<NestedCustomRecord>(Assert.Single(record.InterfaceRecords));
            Assert.Equal("third", third.Value);
            Assert.Equal("interface-list", third.ExtensionData!["$via"].GetString());
            NestedCustomRecord fourth = Assert.IsType<NestedCustomRecord>(record.InterfaceDictionary["entry"]);
            Assert.Equal("fourth", fourth.Value);
            Assert.Equal("interface-dictionary", fourth.ExtensionData!["$via"].GetString());
        }
    }

    [Fact]
    public void PolymorphicCollectionEnvelopeNormalizesNestedMetadataWithoutReferenceHandling()
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = TolerantTestContext.Default);

        foreach (string json in new[]
        {
            """{"$type":"test.polymorphicList","$via":"outer","$values":[]}""",
            """{"$type":"test.polymorphicList","$via":"outer","$values":[{"$type":"test.nested","$via":"nested","value":"item"}]}"""
        })
        {
            PolymorphicRecordListBase records = Assert.IsType<PolymorphicRecordList>(
                JsonSerializer.Deserialize<PolymorphicRecordListBase>(json, options));

            if (records.Count != 0)
            {
                NestedCustomRecord record = Assert.IsType<NestedCustomRecord>(Assert.Single(records));
                Assert.Equal("item", record.Value);
                Assert.Equal("nested", record.ExtensionData!["$via"].GetString());
            }
        }
    }

    [Theory]
    [InlineData("""{"$type":"test.polymorphicDictionary","$via":"extension","entry":"value"}""")]
    [InlineData("""{"$via":"extension","entry":"value","$type":"test.polymorphicDictionary"}""")]
    public void PolymorphicDictionaryNormalizesUnknownMetadata(string json)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = TolerantTestContext.Default);

        PolymorphicRecordDictionaryBase dictionary = Assert.IsType<PolymorphicRecordDictionary>(
            JsonSerializer.Deserialize<PolymorphicRecordDictionaryBase>(json, options));

        Assert.Equal("value", dictionary["entry"]);
        Assert.Single(dictionary);
    }

    [Fact]
    public void ReferencePreservingObjectEnvelopeNormalizesUnknownMetadata()
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
        {
            mutable.ReferenceHandler = ReferenceHandler.Preserve;
            mutable.TypeInfoResolver = TolerantTestContext.Default;
        });
        const string json = """
            {"$id":"1","$via":"outer","record":{"$type":"test.nested","value":"item"}}
            """;

        ReferenceEnvelopeRecord envelope = Assert.IsType<ReferenceEnvelopeRecord>(
            JsonSerializer.Deserialize<ReferenceEnvelopeRecord>(json, options));

        NestedCustomRecord record = Assert.IsType<NestedCustomRecord>(envelope.Record);
        Assert.Equal("item", record.Value);
        Assert.Equal("outer", envelope.ExtensionData!["$via"].GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptionsRegisteredConvertersForwardSdkOptionsForNestedPolymorphicValues(bool preserveReferences)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
        {
            mutable.TypeInfoResolver = TolerantTestContext.Default;
            mutable.ReferenceHandler = preserveReferences ? ReferenceHandler.Preserve : null;
            mutable.Converters.Add(new OptionsPayloadConverter());
        });
        const string json = """
            {"$type":"test.optionsConverter","$via":"outer",
             "payload":{"record":{"$type":"test.nested","$via":"inner","value":"nested"}}}
            """;

        OptionsConverterRecord record = Assert.IsType<OptionsConverterRecord>(
            JsonSerializer.Deserialize<OptionsConverterRecordBase>(json, options));
        NestedCustomRecord nested = Assert.IsType<NestedCustomRecord>(record.Payload.Record);
        Assert.Equal("outer", record.ExtensionData!["$via"].GetString());
        Assert.Equal("nested", nested.Value);
        Assert.Equal("inner", nested.ExtensionData!["$via"].GetString());
    }

    [Fact]
    public void TemporaryMetadataAliasesDoNotSatisfyMissingRequiredProperties()
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = TolerantTestContext.Default);
        const string missingRequired = """{"$type":"test.aliasCollision","$via":"extension"}""";
        const string requiredPresent = """{"$type":"test.aliasCollision","__idunno_metadata_0":"present","$via":"extension"}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AliasCollisionRecordBase>(missingRequired, options));
        AliasCollisionRecord record = Assert.IsType<AliasCollisionRecord>(
            JsonSerializer.Deserialize<AliasCollisionRecordBase>(requiredPresent, options));
        Assert.Equal("present", record.RequiredValue);
        Assert.Equal("extension", record.ExtensionData!["$via"].GetString());
    }

    [Fact]
    public void DuplicateUnknownMetadataPropertiesKeepTheLastValueWithSortedExtensionData()
    {
        const string json = """
            {"$type":"test.sortedExtension","$via":"0","$via":"1","$via":"2","$via":"3","$via":"4","$via":"5","$via":"6","$via":"7","$via":"8","$via":"9","$via":"10","$via":"11"}
            """;
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = TolerantTestContext.Default);

        SortedExtensionRecord record = Assert.IsType<SortedExtensionRecord>(
            JsonSerializer.Deserialize<SortedExtensionRecordBase>(json, options));

        Assert.Equal("11", record.ExtensionData!["$via"].GetString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("__idunno_metadata_0")]
    [InlineData("__idunno_\u0000metadata_0")]
    [InlineData("__IDUNNO_METADATA_0")]
    public void TemporaryAliasesPreserveOrdinaryFieldsInSortedExtensionData(string ordinaryName)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = TolerantTestContext.Default);
        string json = $$"""
            {"$type":"test.sortedExtension",{{JsonSerializer.Serialize(ordinaryName)}}:"original","$via":"extension"}
            """;

        SortedExtensionRecord record = Assert.IsType<SortedExtensionRecord>(
            JsonSerializer.Deserialize<SortedExtensionRecordBase>(json, options));

        Assert.Equal(2, record.ExtensionData!.Count);
        Assert.Equal("original", record.ExtensionData[ordinaryName].GetString());
        Assert.Equal("extension", record.ExtensionData["$via"].GetString());
    }

    [Theory]
    [InlineData("""{"$type":"test.aliasComparer","$via":"metadata","\u0000$via":"ordinary"}""", "ordinary")]
    [InlineData("""{"$type":"test.aliasComparer","\u0000$via":"ordinary","$via":"metadata"}""", "metadata")]
    public void TemporaryAliasesPreserveComparerEquivalentExtensionDataInSourceOrder(string json, string expected)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = TolerantTestContext.Default);

        AliasComparerRecord record = Assert.IsType<AliasComparerRecord>(
            JsonSerializer.Deserialize<AliasComparerRecordBase>(json, options));

        Assert.Equal(expected, record.ExtensionData["$via"].GetString());
    }

    [Theory]
    [InlineData(4000)]
    [InlineData(8000)]
    public void ManySiblingRecordsRestoreOnlyTheirOwnMetadataAliases(int count)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = TolerantTestContext.Default);
        string json = "[" + string.Join(",", Enumerable.Repeat(
            """{"$type":"test.countedExtension","$via":"extension"}""", count)) + "]";

        List<CountedExtensionRecordBase> records = Assert.IsType<List<CountedExtensionRecordBase>>(
            JsonSerializer.Deserialize<List<CountedExtensionRecordBase>>(json, options));

        Assert.Equal(count, records.Count);
        foreach (CountedExtensionRecordBase item in records)
        {
            CountedExtensionRecord record = Assert.IsType<CountedExtensionRecord>(item);
            CountedExtensionDictionary data = Assert.IsType<CountedExtensionDictionary>(record.ExtensionData);
            Assert.Equal("extension", data["$via"].GetString());
            Assert.Equal(1, data.RemovalCount);
            Assert.InRange(data.LookupCount, 1, 2);
        }
    }

    [Theory]
    [InlineData("$id", false)]
    [InlineData("$ref", false)]
    [InlineData("$values", false)]
    [InlineData("$ref", true)]
    public void IgnoreCyclesDoesNotTreatReferenceMetadataNamesAsReserved(string propertyName, bool includeOtherMetadata)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
        {
            mutable.TypeInfoResolver = TolerantTestContext.Default;
            mutable.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });
        string otherMetadata = includeOtherMetadata ? ",\"$via\":\"extension\"" : string.Empty;
        string json = $$"""{"$type":"test.nested","value":"record","{{propertyName}}":"ordinary"{{otherMetadata}}}""";

        NestedCustomRecord record = Assert.IsType<NestedCustomRecord>(
            JsonSerializer.Deserialize<NestedCustomRecordBase>(json, options));

        Assert.Equal("ordinary", record.ExtensionData![propertyName].GetString());
        if (includeOtherMetadata)
        {
            Assert.Equal("extension", record.ExtensionData["$via"].GetString());
        }
    }

    [Fact]
    public void ReferencePreserveKeepsSharedPolymorphicReferencesAcrossSiblings()
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
        {
            mutable.TypeInfoResolver = TolerantTestContext.Default;
            mutable.ReferenceHandler = ReferenceHandler.Preserve;
        });
        NestedCustomRecord shared = new() { Value = "shared" };
        shared.ExtensionData = new()
        {
            ["$via"] = JsonSerializer.SerializeToElement("extension")
        };
        CollectionRecord record = new();
        record.Records.Add(shared);
        record.Records.Add(shared);

        string json = JsonSerializer.Serialize(record, options);
        CollectionRecord roundTripped = Assert.IsType<CollectionRecord>(JsonSerializer.Deserialize<CollectionRecord>(json, options));

        Assert.Contains("\"$ref\"", json, StringComparison.Ordinal);
        Assert.Same(roundTripped.Records[0], roundTripped.Records[1]);
        Assert.Equal("extension", Assert.IsType<NestedCustomRecord>(roundTripped.Records[0]).ExtensionData!["$via"].GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReferencePreserveNormalizesRootCollectionsAndReferenceEnvelopes(bool dictionary)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
        {
            mutable.TypeInfoResolver = TolerantTestContext.Default;
            mutable.ReferenceHandler = ReferenceHandler.Preserve;
        });
        NestedCustomRecord shared = new()
        {
            Value = "shared",
            ExtensionData = new() { ["$via"] = JsonSerializer.SerializeToElement("extension") }
        };
        if (dictionary)
        {
            Dictionary<string, NestedCustomRecordBase> records = new() { ["first"] = shared, ["second"] = shared };
            string json = JsonSerializer.Serialize(records, options);
            Dictionary<string, NestedCustomRecordBase> result = Assert.IsType<Dictionary<string, NestedCustomRecordBase>>(
                JsonSerializer.Deserialize<Dictionary<string, NestedCustomRecordBase>>(json, options));
            Assert.Same(result["first"], result["second"]);
            Assert.Equal("extension", Assert.IsType<NestedCustomRecord>(result["first"]).ExtensionData!["$via"].GetString());
        }
        else
        {
            List<NestedCustomRecordBase> records = [shared, shared];
            string json = JsonSerializer.Serialize(records, options);
            List<NestedCustomRecordBase> result = Assert.IsType<List<NestedCustomRecordBase>>(
                JsonSerializer.Deserialize<List<NestedCustomRecordBase>>(json, options));
            Assert.Same(result[0], result[1]);
            Assert.Equal("extension", Assert.IsType<NestedCustomRecord>(result[0]).ExtensionData!["$via"].GetString());
        }
    }

    [Fact]
    public void CustomConverterOutputHonorsConfiguredMaximumDepth()
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
        {
            mutable.TypeInfoResolver = TolerantTestContext.Default;
            mutable.MaxDepth = 128;
            mutable.Converters.Add(new DeepPayloadConverter());
        });

        string json = JsonSerializer.Serialize<DeepPayloadRecordBase>(new DeepPayloadRecord(), options);

        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 128 });
        JsonElement value = document.RootElement.GetProperty("value");
        for (int depth = 0; depth < 80; depth++)
        {
            value = value[0];
        }
        Assert.Equal("deep", value.GetString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PolymorphicRootConvertersRetainNullHandling(bool rejectNull, bool preserveReferences)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
        {
            mutable.TypeInfoResolver = TolerantTestContext.Default;
            mutable.ReferenceHandler = preserveReferences ? ReferenceHandler.Preserve : null;
            mutable.Converters.Add(new RootNullConverter(rejectNull));
        });

        if (rejectNull)
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NestedCustomRecordBase>("null", options));
            Assert.Throws<JsonException>(() => JsonSerializer.Serialize<NestedCustomRecordBase>(null!, options));
        }
        else
        {
            NestedCustomRecord record = Assert.IsType<NestedCustomRecord>(
                JsonSerializer.Deserialize<NestedCustomRecordBase>("null", options));
            Assert.Equal("read-null", record.Value);
            Assert.Equal("\"write-null\"", JsonSerializer.Serialize<NestedCustomRecordBase>(null!, options));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NormalizationRetainsRawOpaqueExtensionValues(bool preserveReferences)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
        {
            mutable.TypeInfoResolver = TolerantTestContext.Default;
            mutable.ReferenceHandler = preserveReferences ? ReferenceHandler.Preserve : null;
        });
        const string raw = """{ "x": 1, "escaped": "\u0061", "items": [ 1, 2 ] }""";
        string json = $$"""{"$type":"test.nested","value":"nested","ordinary":{{raw}},"$via":{{raw}}}""";

        NestedCustomRecord record = Assert.IsType<NestedCustomRecord>(
            JsonSerializer.Deserialize<NestedCustomRecordBase>(json, options));

        Assert.Equal(raw, record.ExtensionData!["ordinary"].GetRawText());
        Assert.Equal(raw, record.ExtensionData["$via"].GetRawText());
    }

    [Theory]
    [InlineData(1100, false)]
    [InlineData(1100, true)]
    [InlineData(1200, true)]
    public void NormalizationAndPropertyBridgeHonorDeepReadBudgets(int maxDepth, bool customConverter)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
        {
            mutable.TypeInfoResolver = TolerantTestContext.Default;
            mutable.MaxDepth = maxDepth;
        });
        int depth = maxDepth - 1;
        string value = new string('[', depth) + "\"deep\"" + new string(']', depth);
        string discriminator = customConverter ? "test.deepPayload" : "test.deepNormalization";
        string json = $$"""{"$type":"{{discriminator}}","$via":"extension","value":{{value}}}""";

        if (customConverter)
        {
            DeepPayloadRecord record = Assert.IsType<DeepPayloadRecord>(
                JsonSerializer.Deserialize<DeepPayloadRecordBase>(json, options));
            Assert.Equal(depth, record.Value.Depth);
        }
        else
        {
            DeepNormalizationRecord record = Assert.IsType<DeepNormalizationRecord>(
                JsonSerializer.Deserialize<DeepNormalizationRecordBase>(json, options));
            JsonElement leaf = record.Value;
            for (int index = 0; index < depth; index++)
            {
                leaf = leaf[0];
            }
            Assert.Equal("deep", leaf.GetString());
            Assert.Equal("extension", record.ExtensionData!["$via"].GetString());
        }

        string tooDeep = $$"""{"$type":"{{discriminator}}","$via":"extension","value":[{{value}}]}""";
        if (customConverter)
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DeepPayloadRecordBase>(tooDeep, options));
        }
        else
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DeepNormalizationRecordBase>(tooDeep, options));
        }
    }

    [Fact]
    public void MetadataFreeStrongReferencesAvoidScalarForwardingAllocations()
    {
        (long readBytes, long writeBytes) = MeasureAllocations(AtProtoJsonSerializerOptions.Default);
        (long directReadBytes, long directWriteBytes) = MeasureAllocations(AtProtoJsonSerializerOptions.CreateMutableOptions());

        Assert.True(readBytes <= directReadBytes + 512,
            $"Strong-reference decoding allocated {readBytes} bytes per call versus {directReadBytes} on the direct path.");
        Assert.True(writeBytes <= directWriteBytes + 256,
            $"Strong-reference encoding allocated {writeBytes} bytes per call versus {directWriteBytes} on the direct path.");

        static (long ReadBytes, long WriteBytes) MeasureAllocations(JsonSerializerOptions options)
        {
            StrongReference record = Assert.IsType<StrongReference>(
                JsonSerializer.Deserialize<SubjectType>(SubjectWithTrailingTypeDiscriminator, options));
            for (int iteration = 0; iteration < 10; iteration++)
            {
                _ = JsonSerializer.Deserialize<SubjectType>(SubjectWithTrailingTypeDiscriminator, options);
                _ = JsonSerializer.Serialize<SubjectType>(record, options);
            }

            long beforeRead = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 100; iteration++)
            {
                _ = JsonSerializer.Deserialize<SubjectType>(SubjectWithTrailingTypeDiscriminator, options);
            }
            long allocatedRead = (GC.GetAllocatedBytesForCurrentThread() - beforeRead) / 100;
            long beforeWrite = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 100; iteration++)
            {
                _ = JsonSerializer.Serialize<SubjectType>(record, options);
            }
            long allocatedWrite = (GC.GetAllocatedBytesForCurrentThread() - beforeWrite) / 100;

            return (allocatedRead, allocatedWrite);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DecoratedAgentOptionsDoNotReenterTheirLiveResolver(bool registerCustomContext)
    {
        JsonOptions supplied = new();
        if (registerCustomContext)
        {
            supplied.JsonSerializerOptions.TypeInfoResolverChain.Add(TolerantTestContext.Default);
        }
        int callbacks = 0;
        supplied.JsonSerializerOptions.TypeInfoResolver = supplied.JsonSerializerOptions.TypeInfoResolver!.WithAddedModifier(info =>
        {
            if (info.Type == typeof(StrongReference))
            {
                info.OnDeserialized = _ => callbacks++;
            }
        });
        using AtProtoAgent agent = new(new Uri("https://test.invalid"), new AtProtoAgentOptions { HttpJsonOptions = supplied });

        Assert.IsType<StrongReference>(JsonSerializer.Deserialize<SubjectType>(
            SubjectWithTrailingTypeDiscriminator, agent.JsonOptions.JsonSerializerOptions));
        Assert.Equal(1, callbacks);
        if (registerCustomContext)
        {
            NestedCustomRecord record = Assert.IsType<NestedCustomRecord>(JsonSerializer.Deserialize<NestedCustomRecordBase>(
                """{"$type":"test.nested","$via":"extension","value":"custom"}""", agent.JsonOptions.JsonSerializerOptions));
            Assert.Equal("extension", record.ExtensionData!["$via"].GetString());
        }
    }

    [Fact]
    public void AgentResolverSnapshotIsUnaffectedByLaterCallerResolverChanges()
    {
        JsonOptions supplied = new();
        supplied.JsonSerializerOptions.TypeInfoResolverChain.Add(TolerantTestContext.Default);
        int callbacks = 0;
        supplied.JsonSerializerOptions.TypeInfoResolver =
            supplied.JsonSerializerOptions.TypeInfoResolver!.WithAddedModifier(info =>
            {
                if (info.Type == typeof(NestedCustomRecord))
                {
                    info.OnDeserialized = _ => callbacks++;
                }
            });
        using AtProtoAgent agent = new(new Uri("https://test.invalid"), new AtProtoAgentOptions { HttpJsonOptions = supplied });

        supplied.JsonSerializerOptions.TypeInfoResolver =
            ChainedTestContext.Default.WithAddedModifier(_ => { });
        NestedCustomRecord record = Assert.IsType<NestedCustomRecord>(JsonSerializer.Deserialize<NestedCustomRecordBase>(
            """{"$type":"test.nested","$via":"extension","value":"snapshot"}""", agent.JsonOptions.JsonSerializerOptions));

        Assert.Equal("snapshot", record.Value);
        Assert.Equal("extension", record.ExtensionData!["$via"].GetString());
        Assert.Equal(1, callbacks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustomReferenceHandlersPreserveSharedRecordsAndMetadata(bool rootCollection)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
        {
            mutable.TypeInfoResolver = TolerantTestContext.Default;
            mutable.ReferenceHandler = new TestReferenceHandler();
        });
        NestedCustomRecord shared = new()
        {
            Value = "shared",
            ExtensionData = new() { ["$via"] = JsonSerializer.SerializeToElement("extension") }
        };
        if (rootCollection)
        {
            List<NestedCustomRecordBase> records = [shared, shared];
            string json = JsonSerializer.Serialize(records, options);
            List<NestedCustomRecordBase> result = Assert.IsType<List<NestedCustomRecordBase>>(
                JsonSerializer.Deserialize<List<NestedCustomRecordBase>>(json, options));
            Assert.Same(result[0], result[1]);
            Assert.Equal("extension", Assert.IsType<NestedCustomRecord>(result[0]).ExtensionData!["$via"].GetString());
        }
        else
        {
            CollectionRecord record = new() { Records = [shared, shared] };
            string json = JsonSerializer.Serialize<CollectionRecordBase>(record, options);
            CollectionRecord result = Assert.IsType<CollectionRecord>(JsonSerializer.Deserialize<CollectionRecordBase>(json, options));
            Assert.Same(result.Records[0], result.Records[1]);
            Assert.Equal("extension", Assert.IsType<NestedCustomRecord>(result.Records[0]).ExtensionData!["$via"].GetString());
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CustomDictionaryKeyConvertersKeepTheirPropertyNameHooks(bool normalize, bool preserveReferences)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
        {
            mutable.TypeInfoResolver = TolerantTestContext.Default;
            mutable.ReferenceHandler = preserveReferences ? new TestReferenceHandler() : null;
        });
        string metadata = normalize ? "\"$via\":\"extension\"," : "";
        string json = $$"""{"$type":"test.keys",{{metadata}}"values":{"key:first":"value"},"key":"plain:first"}""";
        KeyRecord record = Assert.IsType<KeyRecord>(JsonSerializer.Deserialize<KeyRecordBase>(json, options));
        Assert.Equal("value", record.Values[new TestKey("first")]);
        Assert.Equal(new TestKey("first"), record.Key);
        string serialized = JsonSerializer.Serialize<KeyRecordBase>(record, options);
        Assert.Contains("\"key:first\":\"value\"", serialized, StringComparison.Ordinal);
        Assert.Contains("\"plain:first\"", serialized, StringComparison.Ordinal);
        KeyRecord result = Assert.IsType<KeyRecord>(JsonSerializer.Deserialize<KeyRecordBase>(serialized, options));
        Assert.Equal("value", result.Values[new TestKey("first")]);
    }

    [Fact]
    public void RuntimeDiscoveredDictionaryKeyConvertersKeepTheirPropertyNameHooks()
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = TolerantTestContext.Default);
        KeyRecord record = new()
        {
            RuntimeValue = new Dictionary<TestKey, string> { [new TestKey("runtime")] = "value" }
        };

        string json = JsonSerializer.Serialize<KeyRecordBase>(record, options);

        Assert.Contains("\"key:runtime\":\"value\"", json, StringComparison.Ordinal);
    }

#if NET10_0_OR_GREATER
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NormalizationHonorsDisallowedDuplicateProperties(bool extensionData)
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
        {
            mutable.AllowDuplicateProperties = false;
            mutable.TypeInfoResolver = TolerantTestContext.Default;
        });
        string json = extensionData
            ? """{"$type":"test.nested","$via":"first","$via":"second","value":"nested"}"""
            : """{"$type":"test.keys","$via":"first","$via":"second","values":{}}""";

        if (extensionData)
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NestedCustomRecordBase>(json, options));
        }
        else
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<KeyRecordBase>(json, options));
        }
    }
#endif

    [Fact]
    public void TolerantContractProbeHonorsCallerRegisteredConverters()
    {
        JsonOptions jsonOptions = new();
        jsonOptions.JsonSerializerOptions.Converters.Add(new ConflictingContractConverter());

        ConflictingContract result = Assert.IsType<ConflictingContract>(
            JsonSerializer.Deserialize<ConflictingContract>(
                """{"custom":"wire-format"}""",
                jsonOptions.JsonSerializerOptions));

        Assert.Equal("wire-format", result.Value);
    }

    [Fact]
    public void NullableStructContractsAreTraversedForNestedPolymorphicValues()
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = TolerantTestContext.Default);
        const string json = """
            {"$type":"test.nullableContainer","$via":"outer","payload":{"record":{"$type":"test.nested","$via":"inner","value":"nested"}}}
            """;

        NullableContainerRecord record = Assert.IsType<NullableContainerRecord>(
            JsonSerializer.Deserialize<NullableContainerBase>(json, options));

        Assert.Equal("outer", record.ExtensionData!["$via"].GetString());
        Assert.Equal("nested", Assert.IsType<NestedCustomRecord>(record.Payload!.Value.Record).Value);
        Assert.Equal("inner", Assert.IsType<NestedCustomRecord>(record.Payload.Value.Record).ExtensionData!["$via"].GetString());
    }

    [Fact]
    public void IgnoreCyclesPreservesCycleStateAcrossPolymorphicCollectionBoundary()
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
        {
            mutable.TypeInfoResolver = TolerantTestContext.Default;
            mutable.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });
        List<CycleNodeBase> nodes = [];
        CycleNode node = new() { Value = "node", Parent = nodes };
        node.ExtensionData = new() { ["$via"] = JsonSerializer.SerializeToElement("extension") };
        nodes.Add(node);

        string json = JsonSerializer.Serialize(nodes, options);
        List<CycleNodeBase> result = Assert.IsType<List<CycleNodeBase>>(JsonSerializer.Deserialize<List<CycleNodeBase>>(json, options));
        CycleNode decoded = Assert.IsType<CycleNode>(Assert.Single(result));

        Assert.Null(decoded.Parent);
        Assert.Equal("extension", decoded.ExtensionData!["$via"].GetString());
        Assert.Contains("\"parent\":null", json, StringComparison.Ordinal);
    }

    [Fact]
    public void IgnoredPropertiesAreNotTraversedWhenCollectingDictionaryKeyTypes()
    {
        JsonSerializerOptions options = AtProtoJsonSerializerOptions.Create(mutable =>
            mutable.TypeInfoResolver = new RejectingTypeInfoResolver(typeof(IgnoredPayload)));

        CycleNode record = Assert.IsType<CycleNode>(JsonSerializer.Deserialize<CycleNodeBase>(
            """{"$type":"test.cycleNode","$via":"extension","value":"node"}""",
            options));

        Assert.Equal("node", record.Value);
        Assert.Equal("extension", record.ExtensionData!["$via"].GetString());
    }
}

internal sealed class RejectingTypeInfoResolver(Type rejectedType) : IJsonTypeInfoResolver
{
    private readonly DefaultJsonTypeInfoResolver _resolver = new();

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) =>
        type == rejectedType
            ? throw new InvalidOperationException($"Metadata for {type} must not be requested.")
            : _resolver.GetTypeInfo(type, options);
}

internal sealed class CustomCompositeTypeInfoResolver : IJsonTypeInfoResolver
{
    private readonly List<IJsonTypeInfoResolver> _resolvers;

    internal CustomCompositeTypeInfoResolver(IJsonTypeInfoResolver resolver)
    {
        _resolvers = [resolver];
    }

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        foreach (IJsonTypeInfoResolver resolver in _resolvers)
        {
            JsonTypeInfo? typeInfo = resolver.GetTypeInfo(type, options);
            if (typeInfo is null)
            {
                continue;
            }

            if (type == typeof(ChainedTestRecord))
            {
                Assert.Single(typeInfo.Properties).Name = "customValue";
            }

            return typeInfo;
        }

        return null;
    }
}

internal sealed class IgnoredPayload;

internal sealed class ConflictingContract
{
    [JsonPropertyName("same")]
    public string First { get; set; } = string.Empty;

    [JsonPropertyName("same")]
    public string Second { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

internal sealed class ConflictingContractConverter : JsonConverter<ConflictingContract>
{
    public override ConflictingContract Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        return new ConflictingContract { Value = document.RootElement.GetProperty("custom").GetString()! };
    }

    public override void Write(Utf8JsonWriter writer, ConflictingContract value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("custom", value.Value);
        writer.WriteEndObject();
    }
}

internal sealed class TestReferenceHandler : ReferenceHandler
{
    public override ReferenceResolver CreateResolver() => new TestReferenceResolver();
}

internal sealed class TestReferenceResolver : ReferenceResolver
{
    private readonly Dictionary<string, object> _readReferences = new(StringComparer.Ordinal);
    private readonly Dictionary<object, string> _writeReferences = new(ReferenceEqualityComparer.Instance);

    public override void AddReference(string referenceId, object value) => _readReferences.Add(referenceId, value);

    public override object ResolveReference(string referenceId) => _readReferences[referenceId];

    public override string GetReference(object value, out bool alreadyExists)
    {
        alreadyExists = _writeReferences.TryGetValue(value, out string? id);
        if (alreadyExists)
        {
            return id!;
        }
        id = (_writeReferences.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _writeReferences.Add(value, id);
        return id;
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(KeyRecord), "test.keys")]
internal abstract class KeyRecordBase;

internal sealed class KeyRecord : KeyRecordBase
{
    public Dictionary<TestKey, string> Values { get; set; } = [];

    public TestKey Key { get; set; } = new("default");

    public object? RuntimeValue { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(NullableContainerRecord), "test.nullableContainer")]
internal abstract class NullableContainerBase;

internal sealed class NullableContainerRecord : NullableContainerBase
{
    public NullablePayload? Payload { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal readonly record struct NullablePayload(NestedCustomRecordBase? Record);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CycleNode), "test.cycleNode")]
internal abstract class CycleNodeBase;

internal sealed class CycleNode : CycleNodeBase
{
    public string Value { get; set; } = string.Empty;

    public object? Parent { get; set; }

    [JsonIgnore]
    public Dictionary<string, IgnoredPayload> IgnoredValues { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

[JsonConverter(typeof(TestKeyConverter))]
internal sealed record TestKey(string Value);

internal sealed class TestKeyConverter : JsonConverter<TestKey>
{
    public override TestKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString()!["plain:".Length..]);

    public override void Write(Utf8JsonWriter writer, TestKey value, JsonSerializerOptions options) =>
        writer.WriteStringValue("plain:" + value.Value);

    public override TestKey ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString()!["key:".Length..]);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, TestKey value, JsonSerializerOptions options) =>
        writer.WritePropertyName("key:" + value.Value);
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

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CollectionRecord), "test.collections")]
internal abstract class CollectionRecordBase;

internal sealed class CollectionRecord : CollectionRecordBase
{
    public DerivedRecordList Records { get; set; } = [];

    public DerivedRecordDictionary Dictionary { get; set; } = [];

    public IEnumerable<NestedCustomRecordBase> InterfaceRecords { get; set; } = [];

    public IDictionary<string, NestedCustomRecordBase> InterfaceDictionary { get; set; } = new Dictionary<string, NestedCustomRecordBase>();
}

internal sealed class DerivedRecordList : List<NestedCustomRecordBase>;

internal sealed class DerivedRecordDictionary : Dictionary<string, NestedCustomRecordBase>;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(PolymorphicRecordList), "test.polymorphicList")]
internal abstract class PolymorphicRecordListBase : List<NestedCustomRecordBase>;

internal sealed class PolymorphicRecordList : PolymorphicRecordListBase;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(PolymorphicRecordDictionary), "test.polymorphicDictionary")]
internal abstract class PolymorphicRecordDictionaryBase : Dictionary<string, string>;

internal sealed class PolymorphicRecordDictionary : PolymorphicRecordDictionaryBase;

internal sealed class ReferenceEnvelopeRecord
{
    public NestedCustomRecordBase? Record { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(OptionsConverterRecord), "test.optionsConverter")]
internal abstract class OptionsConverterRecordBase;

internal sealed class OptionsConverterRecord : OptionsConverterRecordBase
{
    public OptionsPayload Payload { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class OptionsPayload
{
    public NestedCustomRecordBase? Record { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AliasCollisionRecord), "test.aliasCollision")]
internal abstract class AliasCollisionRecordBase;

internal sealed class AliasCollisionRecord : AliasCollisionRecordBase
{
    [JsonPropertyName("__idunno_metadata_0")]
    [JsonRequired]
    public string RequiredValue { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SortedExtensionRecord), "test.sortedExtension")]
internal abstract class SortedExtensionRecordBase;

internal sealed class SortedExtensionRecord : SortedExtensionRecordBase
{
    [JsonExtensionData]
    public SortedDictionary<string, JsonElement>? ExtensionData { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AliasComparerRecord), "test.aliasComparer")]
internal abstract class AliasComparerRecordBase;

internal sealed class AliasComparerRecord : AliasComparerRecordBase
{
    [JsonExtensionData]
    public AliasComparerExtensionData ExtensionData { get; set; } = new();
}

internal sealed class AliasComparerExtensionData : Dictionary<string, JsonElement>
{
    public AliasComparerExtensionData() : base(new AliasComparer())
    {
    }
}

internal sealed class AliasComparer : IEqualityComparer<string>
{
    public bool Equals(string? x, string? y) =>
        string.Equals(Normalize(x), Normalize(y), StringComparison.Ordinal);

    public int GetHashCode(string obj) => StringComparer.Ordinal.GetHashCode(Normalize(obj)!);

    private static string? Normalize(string? value) =>
        value?.StartsWith('\0') is true ? value[1..] : value;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CountedExtensionRecord), "test.countedExtension")]
internal abstract class CountedExtensionRecordBase;

internal sealed class CountedExtensionRecord : CountedExtensionRecordBase
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = new CountedExtensionDictionary();
}

internal sealed class CountedExtensionDictionary : Dictionary<string, JsonElement>, IDictionary<string, JsonElement>
{
    public int RemovalCount { get; private set; }

    public int LookupCount { get; private set; }

    bool IDictionary<string, JsonElement>.Remove(string key)
    {
        RemovalCount++;
        return Remove(key);
    }

    bool IDictionary<string, JsonElement>.TryGetValue(string key, out JsonElement value)
    {
        LookupCount++;
        return TryGetValue(key, out value);
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DeepPayloadRecord), "test.deepPayload")]
internal abstract class DeepPayloadRecordBase;

internal sealed class DeepPayloadRecord : DeepPayloadRecordBase
{
    [JsonConverter(typeof(DeepPayloadConverter))]
    public DeepPayload Value { get; set; } = new();
}

internal sealed class DeepPayload
{
    public int Depth { get; set; } = 80;
}

internal sealed class DeepPayloadConverter : JsonConverter<DeepPayload>
{
    public override DeepPayload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement value = document.RootElement;
        int depth = 0;
        while (value.ValueKind == JsonValueKind.Array)
        {
            depth++;
            value = value[0];
        }
        return new() { Depth = depth };
    }

    public override void Write(Utf8JsonWriter writer, DeepPayload value, JsonSerializerOptions options)
    {
        for (int depth = 0; depth < 80; depth++)
        {
            writer.WriteStartArray();
        }
        writer.WriteStringValue("deep");
        for (int depth = 0; depth < 80; depth++)
        {
            writer.WriteEndArray();
        }
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DeepNormalizationRecord), "test.deepNormalization")]
internal abstract class DeepNormalizationRecordBase;

internal sealed class DeepNormalizationRecord : DeepNormalizationRecordBase
{
    public JsonElement Value { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class RootNullConverter(bool rejectNull) : JsonConverter<NestedCustomRecordBase>
{
    public override bool HandleNull => true;

    public override NestedCustomRecordBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        rejectNull ? throw new JsonException("Null is not permitted.") : new NestedCustomRecord { Value = "read-null" };

    public override void Write(Utf8JsonWriter writer, NestedCustomRecordBase value, JsonSerializerOptions options)
    {
        if (rejectNull)
        {
            throw new JsonException("Null is not permitted.");
        }
        writer.WriteStringValue("write-null");
    }
}

internal sealed class OptionsPayloadConverter : JsonConverter<OptionsPayload>
{
    public override OptionsPayload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        return new()
        {
            Record = JsonSerializer.Deserialize<NestedCustomRecordBase>(
                document.RootElement.GetProperty("record"), options)
        };
    }

    public override void Write(Utf8JsonWriter writer, OptionsPayload value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("record");
        JsonSerializer.Serialize(writer, value.Record, options);
        writer.WriteEndObject();
    }
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

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SentinelNullRecord), "test.nullSentinel")]
[JsonDerivedType(typeof(DefaultNullRecord), "test.nullDefault")]
[JsonDerivedType(typeof(RejectedNullRecord), "test.nullRejected")]
internal abstract class NullPolicyRecordBase;

internal sealed class SentinelNullRecord : NullPolicyRecordBase
{
    public SentinelNullValue? Value { get; set; }
}

internal sealed class DefaultNullRecord : NullPolicyRecordBase
{
    public DefaultNullValue? Value { get; set; }
}

internal sealed class RejectedNullRecord : NullPolicyRecordBase
{
    public RejectedNullValue? Value { get; set; }
}

[JsonConverter(typeof(SentinelNullConverter))]
internal sealed record SentinelNullValue(string Value);

[JsonConverter(typeof(DefaultNullConverter))]
internal sealed record DefaultNullValue(string Value);

[JsonConverter(typeof(RejectedNullConverter))]
internal sealed record RejectedNullValue(string Value);

internal sealed class SentinelNullConverter : JsonConverter<SentinelNullValue>
{
    public override bool HandleNull => true;

    public override SentinelNullValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.TokenType == JsonTokenType.Null ? "read-null" : reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, SentinelNullValue value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value?.Value ?? "write-null");
}

internal sealed class DefaultNullConverter : JsonConverter<DefaultNullValue>
{
    public override DefaultNullValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.TokenType == JsonTokenType.Null ? throw new JsonException("Unexpected null.") : reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, DefaultNullValue value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value?.Value ?? throw new JsonException("Unexpected null."));
}

internal sealed class RejectedNullConverter : JsonConverter<RejectedNullValue>
{
    public override bool HandleNull => true;

    public override RejectedNullValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.TokenType == JsonTokenType.Null ? throw new JsonException("Null is not permitted.") : reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, RejectedNullValue value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value?.Value ?? throw new JsonException("Null is not permitted."));
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(LateBoundRecordBase))]
[JsonSerializable(typeof(LateBoundRecord))]
[JsonSerializable(typeof(CustomStructRecordBase))]
[JsonSerializable(typeof(RecordWithCustomStruct))]
[JsonSerializable(typeof(CustomStructPayload))]
[JsonSerializable(typeof(NestedCustomRecordBase))]
[JsonSerializable(typeof(NestedCustomRecord))]
[JsonSerializable(typeof(CollectionRecordBase))]
[JsonSerializable(typeof(CollectionRecord))]
[JsonSerializable(typeof(PolymorphicRecordListBase))]
[JsonSerializable(typeof(PolymorphicRecordList))]
[JsonSerializable(typeof(PolymorphicRecordDictionaryBase))]
[JsonSerializable(typeof(PolymorphicRecordDictionary))]
[JsonSerializable(typeof(ReferenceEnvelopeRecord))]
[JsonSerializable(typeof(OptionsConverterRecordBase))]
[JsonSerializable(typeof(OptionsConverterRecord))]
[JsonSerializable(typeof(OptionsPayload))]
[JsonSerializable(typeof(PropertyRecordBase))]
[JsonSerializable(typeof(PropertyRecord))]
[JsonSerializable(typeof(PropertyStructPayload))]
[JsonSerializable(typeof(NullPolicyRecordBase))]
[JsonSerializable(typeof(SentinelNullRecord))]
[JsonSerializable(typeof(DefaultNullRecord))]
[JsonSerializable(typeof(RejectedNullRecord))]
[JsonSerializable(typeof(AliasCollisionRecordBase))]
[JsonSerializable(typeof(AliasCollisionRecord))]
[JsonSerializable(typeof(SortedExtensionRecordBase))]
[JsonSerializable(typeof(SortedExtensionRecord))]
[JsonSerializable(typeof(AliasComparerRecordBase))]
[JsonSerializable(typeof(AliasComparerRecord))]
[JsonSerializable(typeof(DeepPayloadRecordBase))]
[JsonSerializable(typeof(DeepPayloadRecord))]
[JsonSerializable(typeof(DeepPayload))]
[JsonSerializable(typeof(KeyRecordBase))]
[JsonSerializable(typeof(KeyRecord))]
[JsonSerializable(typeof(NullableContainerBase))]
[JsonSerializable(typeof(NullableContainerRecord))]
[JsonSerializable(typeof(NullablePayload))]
[JsonSerializable(typeof(CycleNodeBase))]
[JsonSerializable(typeof(CycleNode))]
[JsonSerializable(typeof(Dictionary<TestKey, string>))]
[JsonSerializable(typeof(List<CycleNodeBase>))]
[JsonSerializable(typeof(DeepNormalizationRecordBase))]
[JsonSerializable(typeof(DeepNormalizationRecord))]
[JsonSerializable(typeof(CountedExtensionRecordBase))]
[JsonSerializable(typeof(CountedExtensionRecord))]
[JsonSerializable(typeof(List<CountedExtensionRecordBase>))]
[JsonSerializable(typeof(List<NestedCustomRecordBase>))]
[JsonSerializable(typeof(Dictionary<string, NestedCustomRecordBase>))]
[ExcludeFromCodeCoverage]
internal sealed partial class TolerantTestContext : JsonSerializerContext
{
}
