// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Admin;
using idunno.AtProto.Repo;

namespace idunno.AtProto.Moderation
{
    /// <summary>
    /// Base record for references.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = false, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
    [JsonDerivedType(typeof(StrongReference), typeDiscriminator: "com.atproto.repo.strongRef")]
    [JsonDerivedType(typeof(RepoReference), typeDiscriminator: "com.atproto.admin.defs#repoRef")]
    public abstract record SubjectType
    {
    }
}
