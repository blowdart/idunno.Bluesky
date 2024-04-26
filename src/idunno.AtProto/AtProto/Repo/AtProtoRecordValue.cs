// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Bluesky;
using idunno.AtProto.Json;

namespace idunno.AtProto.Repo
{
    // Use the custom polymorphic converter because .NET requires the type discriminator to be the first value in the json object.

    [OutOfOrderJsonDerivedType(typeof(BlueskyPostRecordValue), typeDiscriminator: CollectionType.Post)]
    [JsonConverter(typeof(PolymorphicJsonConverter<AtProtoRecordValue>))]
    public record AtProtoRecordValue
    {
        [JsonConstructor]
        public AtProtoRecordValue(DateTime createdAt)
        {
            CreatedAt = createdAt;
        }

        [JsonInclude]
        [JsonRequired]
        public DateTime CreatedAt { get; protected set; }
    }
}
