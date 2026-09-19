// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;

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
}
