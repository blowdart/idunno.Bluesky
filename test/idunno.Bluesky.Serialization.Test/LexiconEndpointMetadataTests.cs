// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.Bluesky.AgeAssurance;
using idunno.Bluesky.Embed.Model;

namespace idunno.Bluesky.Serialization.Test;

public class LexiconEndpointMetadataTests
{
    public static TheoryData<Type> EndpointTypes => new()
    {
        typeof(Config),
        typeof(ConfigRegion),
        typeof(ConfigRegionRule),
        typeof(ConfigRegionRuleDefault),
        typeof(ConfigRegionRuleIfDeclaredOverAge),
        typeof(ConfigRegionRuleIfDeclaredUnderAge),
        typeof(ConfigRegionRuleIfAssuredOverAge),
        typeof(ConfigRegionRuleIfAssuredUnderAge),
        typeof(ConfigRegionRuleIfAccountNewerThan),
        typeof(ConfigRegionRuleIfAccountOlderThan),
        typeof(AgeAssuranceAccess),
        typeof(AgeAssurancePlatform),
        typeof(AdditionalVerificationMethod),
        typeof(StateResult),
        typeof(State),
        typeof(StateMetadata),
        typeof(GetEmbedExternalResponse),
        typeof(EmptyResponse),
        typeof(AtErrorDetail)
    };

    [Theory]
    [MemberData(nameof(EndpointTypes))]
    public void EndpointSerializerPreservesEveryReachableType(Type root)
    {
        JsonSerializerOptions options = BlueskyServer.BlueskyJsonSerializerOptions;
        Assert.All(options.TypeInfoResolverChain, resolver => Assert.IsAssignableFrom<JsonSerializerContext>(resolver));
        HashSet<Type> visited = [];
        Verify(root);

        void Verify(Type type)
        {
            if (!visited.Add(type))
            {
                return;
            }

            JsonTypeInfo metadata = options.GetTypeInfo(type);
            Assert.Equal(type, metadata.Type);
            foreach (JsonPropertyInfo property in metadata.Properties)
            {
                if (property.Get is not null || property.Set is not null)
                {
                    Verify(property.PropertyType);
                }
            }

            if (Nullable.GetUnderlyingType(type) is Type underlying)
            {
                Verify(underlying);
            }
            else if (metadata.Kind is JsonTypeInfoKind.Enumerable or JsonTypeInfoKind.Dictionary)
            {
                foreach (Type argument in type.IsArray ? [type.GetElementType()!] : type.GetGenericArguments())
                {
                    Verify(argument);
                }
            }

            if (metadata.PolymorphismOptions is not null)
            {
                foreach (var derivedType in metadata.PolymorphismOptions.DerivedTypes)
                {
                    Verify(derivedType.DerivedType);
                }
            }
        }
    }

    [Fact]
    public void EndpointSerializerDoesNotFallBackToReflection()
    {
        Assert.Throws<NotSupportedException>(() => BlueskyServer.BlueskyJsonSerializerOptions.GetTypeInfo(typeof(UnregisteredDto)));
    }

    private sealed record UnregisteredDto(string Value);
}
