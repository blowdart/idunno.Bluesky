// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Encapsulates the result from a create operation to the applyWrites API.
    /// </summary>
    public sealed record ApplyWritesCreateResult : ApplyWritesResultBase
    {
        [JsonConstructor]
        internal ApplyWritesCreateResult(AtUri uri, Cid cid, ValidationStatus? validationStatus)
        {
            Uri = uri;
            Cid = cid;
            ValidationStatus = validationStatus;
            StrongReference = new(uri, cid);
        }

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the record the write operation created.
        /// </summary>
        [JsonInclude]
        public AtUri Uri { get; init; }

        /// <summary>
        /// Gets the <see cref="Cid"/> of the record the write operation created.
        /// </summary>
        [JsonInclude]
        public Cid Cid { get; init; }

        /// <summary>
        /// Gets the <see cref="ValidationStatus"/> of the record, if any.
        /// </summary>
        [JsonInclude]
        public ValidationStatus? ValidationStatus { get; init; }

        /// <summary>
        /// The <see cref="StrongReference"/> of the record the write operation created.
        /// </summary>
        [JsonIgnore]
        public StrongReference StrongReference { get; }
    }
}
