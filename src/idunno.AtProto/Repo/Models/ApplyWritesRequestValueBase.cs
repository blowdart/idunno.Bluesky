// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Base record for transactions for the applyWrites API.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = false, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
    [JsonDerivedType(typeof(ApplyWritesCreate), typeDiscriminator: "com.atproto.repo.applyWrites#create")]
    [JsonDerivedType(typeof(ApplyWritesUpdate), typeDiscriminator: "com.atproto.repo.applyWrites#update")]
    [JsonDerivedType(typeof(ApplyWritesDelete), typeDiscriminator: "com.atproto.repo.applyWrites#delete")]
    public record ApplyWritesRequestValueBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="ApplyWritesRequestValueBase"/>
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="rkey"></param>
        protected ApplyWritesRequestValueBase(Nsid collection, RecordKey rkey)
        {
            Collection = collection;
            Rkey = rkey;
        }

        /// <summary>
        /// Gets the <see cref="Nsid"/> of the collection the operation will apply to.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Nsid Collection { get; init; }

        /// <summary>
        /// Gets the <see cref="RecordKey"/> the the operation will apply to.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public RecordKey Rkey { get; init; }
    }
}
