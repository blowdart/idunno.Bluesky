// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Encapsulates the result from a update operation to the applyWrites API.
    /// </summary>

    public sealed record ApplyWritesUpdateResult : ApplyWritesResultBase
    {
        [JsonConstructor]
        internal ApplyWritesUpdateResult(AtUri uri, Cid cid)
        {
            Uri = uri;
            Cid = cid;
            StrongReference = new(uri, cid);
        }

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the record the write operation updated.
        /// </summary>
        [JsonInclude]
        public AtUri Uri { get; init; }

        /// <summary>
        /// Gets the <see cref="Cid"/> of the record the write operation updated.
        /// </summary>
        [JsonInclude]
        public Cid Cid { get; init; }

        /// <summary>
        /// Gets the <see cref="StrongReference"/> of the record the write operation updated.
        /// </summary>
        [JsonIgnore]
        public StrongReference StrongReference { get; }
    }
}
