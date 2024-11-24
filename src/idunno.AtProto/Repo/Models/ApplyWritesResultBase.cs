// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Base abstract type for the results of an applyWrites operation.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators =false, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
    [JsonDerivedType(typeof(ApplyWritesCreateResult), typeDiscriminator: "com.atproto.repo.applyWrites#createResult")]
    [JsonDerivedType(typeof(ApplyWritesUpdateResult), typeDiscriminator: "com.atproto.repo.applyWrites#updateResult")]
    [JsonDerivedType(typeof(ApplyWritesDeleteResult), typeDiscriminator: "com.atproto.repo.applyWrites#deleteResult")]
    public abstract record ApplyWritesResultBase
    {
    }
}
