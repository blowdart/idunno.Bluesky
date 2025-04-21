// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo.Models;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// The result from a Create operation to the applyWrites API.
    /// </summary>
    public sealed record ApplyWritesCreateResult : IApplyWritesResult
    {
        internal ApplyWritesCreateResult(ApplyWritesCreateResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);

            Uri = response.Uri;
            Cid = response.Cid;

            StrongReference = new(Uri, Cid);

            // Attempting to avoid the trimming errors with enum use
            // See https://github.com/dotnet/runtime/issues/114307
            if (response.ValidationStatus is not null)
            {
                switch (response.ValidationStatus.ToUpperInvariant())
                {
                    case "UNKNOWN":
                        ValidationStatus = Repo.ValidationStatus.Unknown;
                        break;

                    case "VALID":
                        ValidationStatus = Repo.ValidationStatus.Valid;
                        break;

                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the record the write operation created.
        /// </summary>
        public AtUri Uri { get; }

        /// <summary>
        /// Gets the <see cref="Cid"/> of the record the write operation created.
        /// </summary>
        public Cid Cid { get; }

        /// <summary>
        /// Gets the <see cref="ValidationStatus"/> of the record, if any.
        /// </summary>
        public ValidationStatus? ValidationStatus { get; }

        /// <summary>
        /// The <see cref="StrongReference"/> of the record the write operation created.
        /// </summary>
        public StrongReference StrongReference { get; }
    }
}
