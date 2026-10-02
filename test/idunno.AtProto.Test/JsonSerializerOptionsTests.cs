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

        Assert.NotSame(first, AtProtoJsonSerializerOptions.Options);
        Assert.NotSame(AtProtoJsonSerializerOptions.Default, first);
        Assert.False(first.IsReadOnly);

        first.Converters.Add(new JsonStringEnumConverter());

        Assert.Empty(AtProtoJsonSerializerOptions.Default.Converters);
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
        Assert.Same(ChainedTestContext.Default, first.TypeInfoResolverChain[0]);
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
