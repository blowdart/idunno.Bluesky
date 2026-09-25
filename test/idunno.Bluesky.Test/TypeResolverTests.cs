// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization.Metadata;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class TypeResolverTests
{
    [Fact]
    public void JsonTypeInfoResolversShouldNotBeMutable()
    {
        IList<IJsonTypeInfoResolver> resolvers = TypeResolver.JsonTypeInfoResolvers;

        Assert.True(resolvers.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => resolvers.Clear());
        Assert.Throws<NotSupportedException>(() => resolvers.Insert(0, TypeResolver.JsonTypeInfoResolver));
        Assert.Throws<NotSupportedException>(() => resolvers.Add(TypeResolver.JsonTypeInfoResolver));
    }

    [Fact]
    public void JsonTypeInfoResolversShouldStillContainTheChainedResolvers()
    {
        IList<IJsonTypeInfoResolver> resolvers = TypeResolver.JsonTypeInfoResolvers;

        Assert.Equal(2, resolvers.Count);
        Assert.Contains(TypeResolver.JsonTypeInfoResolver, resolvers);
        Assert.Contains(AtProto.TypeResolver.JsonTypeInfoResolver, resolvers);
    }
}