// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Feed.Gates;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public partial class BlueskyJsonSerializerOptionsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FactoryFinalizesAfterResolverConfiguration(bool replace)
    {
        JsonSerializerOptions? supplied = null;
        JsonSerializerOptions options = BlueskyJsonSerializerOptions.Create(mutable =>
        {
            supplied = mutable;
            Assert.Single(mutable.Converters);
            ConfigureResolver(mutable, replace);
            mutable.WriteIndented = true;
        });
        Assert.NotNull(supplied);
        Assert.False(supplied.IsReadOnly);
        Assert.True(options.IsReadOnly);
        Assert.True(options.WriteIndented);
        supplied.TypeInfoResolverChain.Clear();
        AssertCustomRecord(options);
        Assert.Throws<InvalidOperationException>(() => options.TypeInfoResolverChain.Clear());
    }

    [Fact]
    public void FactoryRejectsNullConfiguration()
    {
        Assert.Throws<ArgumentNullException>(() => BlueskyJsonSerializerOptions.Create(null!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuilderFinalizesResolversInsertedOrReplacedInTheCallback(bool replace)
    {
        var builder = BlueskyAgent.CreateBuilder();
        builder.ConfigureHttpJsonOptions(options => ConfigureResolver(options.JsonSerializerOptions, replace));
        using BlueskyAgent agent = builder.Build();
        AssertCustomRecord(agent.JsonOptions.JsonSerializerOptions);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AgentFinalizesOptionsCustomizedAfterOptionsConstructionOrBuilderCallback(bool builder, bool replace)
    {
        BlueskyAgentOptions agentOptions = new();
        var agentBuilder = BlueskyAgent.CreateBuilder();
        agentBuilder.ConfigureHttpJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);
        JsonOptions supplied = builder ? Assert.IsType<JsonOptions>(agentBuilder.JsonOptions) : Assert.IsType<JsonOptions>(agentOptions.HttpJsonOptions);
        ConfigureResolver(supplied.JsonSerializerOptions, replace);
        IJsonTypeInfoResolver? resolver = supplied.JsonSerializerOptions.TypeInfoResolver;
        int converterCount = supplied.JsonSerializerOptions.Converters.Count;
        using BlueskyAgent agent = builder ? agentBuilder.Build() : new(agentOptions);
        Assert.False(supplied.JsonSerializerOptions.IsReadOnly);
        Assert.Same(resolver, supplied.JsonSerializerOptions.TypeInfoResolver);
        Assert.Equal(converterCount, supplied.JsonSerializerOptions.Converters.Count);
        supplied.JsonSerializerOptions.TypeInfoResolverChain.Clear();
        Assert.True(agent.JsonOptions.JsonSerializerOptions.IsReadOnly);
        AssertCustomRecord(agent.JsonOptions.JsonSerializerOptions);
        Assert.IsType<Post>(JsonSerializer.Deserialize<Post>(
            """{"$type":"app.bsky.feed.post","$via":"extension","text":"hello","createdAt":"2026-10-13T10:24:57.970Z"}""",
            agent.JsonOptions.JsonSerializerOptions));
    }

    [Fact]
    public void OptionsConstructorDoesNotMutateOrFreezeCallerOptions()
    {
        JsonOptions supplied = new();
        IJsonTypeInfoResolver? resolver = supplied.JsonSerializerOptions.TypeInfoResolver;
        int converterCount = supplied.JsonSerializerOptions.Converters.Count;
        BlueskyAgentOptions options = new(loggerFactory: null, httpJsonOptions: supplied);
        Assert.Same(supplied, options.HttpJsonOptions);
        Assert.Same(resolver, supplied.JsonSerializerOptions.TypeInfoResolver);
        Assert.Equal(converterCount, supplied.JsonSerializerOptions.Converters.Count);
        ConfigureResolver(supplied.JsonSerializerOptions, replace: true);
        using BlueskyAgent agent = new(options);
        Assert.False(supplied.JsonSerializerOptions.IsReadOnly);
        AssertCustomRecord(agent.JsonOptions.JsonSerializerOptions);
    }

    [Fact]
    public void AgentPrefersBlueskySourceGeneratedContractsToCallerReflectionResolver()
    {
        JsonOptions supplied = new();
        using BlueskyAgent agent = new(new BlueskyAgentOptions(loggerFactory: null, httpJsonOptions: supplied));

        JsonTypeInfo profileInfo = agent.JsonOptions.JsonSerializerOptions.GetTypeInfo(typeof(ProfileViewDetailed));
        Assert.Same(SourceGenerationContext.Default, profileInfo.OriginatingResolver);

        ProfileViewDetailed profile = Assert.IsType<ProfileViewDetailed>(JsonSerializer.Deserialize<ProfileViewDetailed>(
            """{"did":"did:plc:test","handle":"example.test","followersCount":"12","followsCount":"34"}""",
            agent.JsonOptions.JsonSerializerOptions));

        Assert.Equal(12, profile.FollowersCount);
        Assert.Equal(34, profile.FollowsCount);
        Assert.False(supplied.JsonSerializerOptions.IsReadOnly);
    }

    [Fact]
    public void AgentPreservesCallerModifiersOnBlueskySourceGeneratedContracts()
    {
        JsonOptions supplied = new();
        int callbacks = 0;
        supplied.JsonSerializerOptions.TypeInfoResolver =
            supplied.JsonSerializerOptions.TypeInfoResolver!.WithAddedModifier(info =>
            {
                if (info.Type == typeof(Post) && info.Kind == JsonTypeInfoKind.Object)
                {
                    info.OnDeserialized = _ => callbacks++;
                }
            });

        using BlueskyAgent agent = new(new BlueskyAgentOptions(loggerFactory: null, httpJsonOptions: supplied));
        JsonTypeInfo profileInfo = agent.JsonOptions.JsonSerializerOptions.GetTypeInfo(typeof(ProfileViewDetailed));
        Assert.Same(SourceGenerationContext.Default, profileInfo.OriginatingResolver);
        Post post = Assert.IsType<Post>(JsonSerializer.Deserialize<Post>(
            """{"$type":"app.bsky.feed.post","text":"hello","createdAt":"2026-10-13T10:24:57.970Z"}""",
            agent.JsonOptions.JsonSerializerOptions));

        Assert.Equal("hello", post.Text);
        Assert.Equal(1, callbacks);
    }

    [Fact]
    public void AgentPreservesModifiersAppliedAfterBlueskyResolverConfiguration()
    {
        BlueskyAgentOptions supplied = new();
        int callbacks = 0;
        JsonSerializerOptions serializerOptions = supplied.HttpJsonOptions!.JsonSerializerOptions;
        serializerOptions.TypeInfoResolver = serializerOptions.TypeInfoResolver!.WithAddedModifier(info =>
        {
            if (info.Type == typeof(Post) && info.Kind == JsonTypeInfoKind.Object)
            {
                info.OnDeserialized = _ => callbacks++;
            }
        });

        using BlueskyAgent agent = new(supplied);
        Post post = Assert.IsType<Post>(JsonSerializer.Deserialize<Post>(
            """{"$type":"app.bsky.feed.post","text":"hello","createdAt":"2026-10-13T10:24:57.970Z"}""",
            agent.JsonOptions.JsonSerializerOptions));

        Assert.Equal("hello", post.Text);
        Assert.Equal(1, callbacks);
    }

    [Fact]
    public void AgentFinalizesOptionsAssignedAfterOptionsConstruction()
    {
        BlueskyAgentOptions options = new();
        JsonOptions supplied = new();
        options.HttpJsonOptions = supplied;
        ConfigureResolver(supplied.JsonSerializerOptions, replace: true);
        using BlueskyAgent agent = new(options);
        Assert.False(supplied.JsonSerializerOptions.IsReadOnly);
        AssertCustomRecord(agent.JsonOptions.JsonSerializerOptions);
    }

    private static void ConfigureResolver(JsonSerializerOptions options, bool replace)
    {
        if (replace)
        {
            options.TypeInfoResolver = FinalizationTestContext.Default;
        }
        else
        {
            options.TypeInfoResolverChain.Insert(0, FinalizationTestContext.Default);
        }
    }

    private static void AssertCustomRecord(JsonSerializerOptions options)
    {
        FinalizationRecord record = Assert.IsType<FinalizationRecord>(JsonSerializer.Deserialize<FinalizationRecordBase>(
            """{"$type":"test.finalization","$via":"custom","value":"configured"}""", options));
        Assert.Equal("configured", record.Value);
        Assert.Equal("custom", record.ExtensionData!["$via"].GetString());
    }

    [Fact]
    public void UnknownDollarPropertyIsRetainedOnPost()
    {
        const string json = """
            {"$type":"app.bsky.feed.post","$via":"kazahana","text":"hello","createdAt":"2026-10-13T10:24:57.970Z","langs":["ja"]}
            """;

        Post post = Assert.IsType<Post>(JsonSerializer.Deserialize<Post>(json, BlueskyJsonSerializerOptions.Default));

        Assert.Equal("hello", post.Text);
        Assert.Equal("kazahana", post.ExtensionData!["$via"].GetString());
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(FinalizationRecord), "test.finalization")]
    internal abstract class FinalizationRecordBase;

    internal sealed class FinalizationRecord : FinalizationRecordBase
    {
        public string Value { get; set; } = string.Empty;

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(FinalizationRecordBase))]
    [JsonSerializable(typeof(FinalizationRecord))]
    internal sealed partial class FinalizationTestContext : JsonSerializerContext;

    // A list rule whose $type discriminator trails the rest of the payload, which is how an
    // AT Protocol server is free to order it.
    private const string ListRuleWithTrailingTypeDiscriminator =
        """
        {"list":"at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.list/3kzmmwnqk2s2b","$type":"app.bsky.feed.threadgate#listRule"}
        """;

    [Fact]
    public void BlueskyJsonSerializerOptionsAllowsOutOfOrderMetadataProperties()
    {
        Assert.True(BlueskyJsonSerializerOptions.Options.AllowOutOfOrderMetadataProperties);
    }

    [Fact]
    public void EveryJsonSerializerOptionsSurfaceInTheAssemblyAllowsOutOfOrderMetadataProperties()
    {
        List<string> offenders = [];

        foreach (Type type in typeof(BlueskyJsonSerializerOptions).Assembly.GetTypes())
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
    public void BlueskyJsonSerializerOptionsReadsAPolymorphicTypeWhoseDiscriminatorComesLast()
    {
        ThreadGateRule? rule = JsonSerializer.Deserialize<ThreadGateRule>(ListRuleWithTrailingTypeDiscriminator, BlueskyJsonSerializerOptions.Options);

        ListRule listRule = Assert.IsType<ListRule>(rule);
        Assert.Equal("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.list/3kzmmwnqk2s2b", listRule.List.ToString());
    }

    [Fact]
    public void DefaultIsTheSameReadOnlyInstanceOnEveryCall()
    {
        JsonSerializerOptions options = BlueskyJsonSerializerOptions.Default;

        Assert.Same(options, BlueskyJsonSerializerOptions.Default);
        Assert.True(options.IsReadOnly);
        Assert.True(options.AllowOutOfOrderMetadataProperties);
        Assert.Throws<InvalidOperationException>(() => options.Converters.Add(new JsonStringEnumConverter()));
    }

    [Fact]
    public void BlueskyServerOptionsAreTheDefaultInstance()
    {
        Assert.Same(BlueskyJsonSerializerOptions.Default, BlueskyServer.BlueskyJsonSerializerOptions);
    }

    [Fact]
    public void OptionsReturnsANewMutableInstanceOnEveryCall()
    {
        JsonSerializerOptions first = BlueskyJsonSerializerOptions.Options;
        int defaultConverterCount = BlueskyJsonSerializerOptions.Default.Converters.Count;

        Assert.NotSame(first, BlueskyJsonSerializerOptions.Options);
        Assert.NotSame(BlueskyJsonSerializerOptions.Default, first);
        Assert.False(first.IsReadOnly);
        Assert.Equal(defaultConverterCount, first.Converters.Count);

        first.Converters.Add(new JsonStringEnumConverter());

        Assert.Equal(defaultConverterCount, BlueskyJsonSerializerOptions.Default.Converters.Count);
    }

    [Fact]
    public void TypeInfoResolverIsTheSameInstanceOnEveryCall()
    {
        Assert.Same(BlueskyJsonSerializerOptions.Default.TypeInfoResolver, BlueskyJsonSerializerOptions.TypeInfoResolver);
        Assert.Same(BlueskyJsonSerializerOptions.TypeInfoResolver, BlueskyJsonSerializerOptions.TypeInfoResolver);
    }

    [Fact]
    public void DefaultReadsAPolymorphicTypeWhoseDiscriminatorComesLast()
    {
        ThreadGateRule? rule = JsonSerializer.Deserialize<ThreadGateRule>(ListRuleWithTrailingTypeDiscriminator, BlueskyJsonSerializerOptions.Default);

        Assert.IsType<ListRule>(rule);
    }
}
