// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.Bluesky.Feed.Gates;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class BlueskyJsonSerializerOptionsTests
{
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
