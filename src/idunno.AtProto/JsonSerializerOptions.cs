// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.
//
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace idunno.AtProto
{
    public partial class AtProtoServer
    {
        /// <summary>
        /// Gets a <see cref="JsonSerializerOptions"/> configured to use Json source generation for AtProto classes
        /// </summary>
        internal static JsonSerializerOptions AtProtoJsonSerializerOptions => new(JsonSerializerDefaults.Web)
        {
            AllowOutOfOrderMetadataProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IgnoreReadOnlyProperties = false,
            TypeInfoResolver = SourceGenerationContext.Default,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        };

        /// <summary>
        /// Gets a <see cref="JsonSerializerOptions"/> without a type resolver.
        /// </summary>
        internal static JsonSerializerOptions DefaultJsonSerializerOptionsWithNoTypeResolution => new(JsonSerializerDefaults.Web)
        {
            AllowOutOfOrderMetadataProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IgnoreReadOnlyProperties = false,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };

        /// <summary>
        /// Gets an instance of <see cref="JsonSerializerOptions" /> with the specified <paramref name="jsonSerializerOptions"/> type resolver chained.
        /// </summary>
        /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to chain type resolution with.</param>
        /// <returns>An instance of <see cref="JsonSerializerOptions" /> with the type information resolvers chained.</returns>
        public static JsonSerializerOptions BuildChainedTypeInfoResolverJsonSerializerOptions(JsonSerializerOptions jsonSerializerOptions)
        {
            ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
            ArgumentNullException.ThrowIfNull(jsonSerializerOptions.TypeInfoResolver);

            return BuildChainedTypeInfoResolverJsonSerializerOptions(jsonSerializerOptions.TypeInfoResolver);
        }

        /// <summary>
        /// Gets an instance of <see cref="JsonSerializerOptions" /> with the specified <paramref name="jsonTypeInfoResolver"/> type resolver chained.
        /// </summary>
        /// <param name="jsonTypeInfoResolver">The type information resolver to insert into the type resolution chain.</param>
        /// <returns>An instance of <see cref="JsonSerializerOptions" /> with the type information resolvers chained.</returns>
        public static JsonSerializerOptions BuildChainedTypeInfoResolverJsonSerializerOptions(IJsonTypeInfoResolver jsonTypeInfoResolver)
        {
            ArgumentNullException.ThrowIfNull(jsonTypeInfoResolver);

            JsonSerializerOptions options = AtProtoJsonSerializerOptions;

            options.TypeInfoResolverChain.Insert(0, jsonTypeInfoResolver);

            return options;
        }
    }
}
