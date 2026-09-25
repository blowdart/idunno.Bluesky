// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Text.Json.Serialization.Metadata;

namespace idunno.Bluesky;

/// <summary>
/// Exposes type resolution for BlueSky JSON types.
/// </summary>
public static class TypeResolver
{
    private static readonly List<IJsonTypeInfoResolver> s_chainedResolvers = [
                    SourceGenerationContext.Default,
                    AtProto.TypeResolver.JsonTypeInfoResolver
        ];

    private static readonly ReadOnlyCollection<IJsonTypeInfoResolver> s_readOnlyChainedResolvers = s_chainedResolvers.AsReadOnly();

    /// <summary>
    /// Gets the default source generation JSON type info resolver for AtProto JSON types.
    /// </summary>
    public static IJsonTypeInfoResolver JsonTypeInfoResolver => SourceGenerationContext.Default;

    /// <summary>
    /// Gets a list of JSON type info resolvers chained for BlueSky and AtProto types.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The returned list is read only. Mutating the resolver chain would change how every
    ///   consumer in the process deserializes AT Protocol and Bluesky types.
    /// </para>
    /// </remarks>
    public static IList<IJsonTypeInfoResolver> JsonTypeInfoResolvers => s_readOnlyChainedResolvers;
}