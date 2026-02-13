// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Drafts
{
    /// <summary>
    /// Encapsulates a reference to an <see cref="AtProtoRecord"/> in a draft post.
    /// </summary>
    [JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(DraftEmbedRecord), typeDiscriminator: "app.bsky.draft.defs#draftEmbedRecord")]
    public record DraftEmbedRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="DraftEmbedRecord"/> with the specified strong reference to a record.
        /// </summary>
        /// <param name="record">The record to embed.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/> is <see langword="null"/>.</exception>
        [JsonConstructor]
        public DraftEmbedRecord(StrongReference record)
        {
            ArgumentNullException.ThrowIfNull(record);
            Record = record;
        }

        /// <summary>
        /// Gets a strong reference to the record to embed.
        /// </summary>
        [JsonRequired]
        public StrongReference Record { get; init; }
    }
}
