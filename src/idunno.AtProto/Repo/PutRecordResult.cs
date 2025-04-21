// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using idunno.AtProto.Repo.Models;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// The result of a call to the putRecord API.
    /// </summary>
    public sealed record PutRecordResult
    {
        internal PutRecordResult(PutRecordResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);

            Uri = response.Uri;
            Cid = response.Cid;
            Commit = response.Commit;

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
        /// Gets the <see cref="AtUri"/> for the newly created record.
        /// </summary>
        public AtUri Uri { get; }

        /// <summary>
        /// Gets the <see cref="Cid"/> for the newly created record.
        /// </summary>
        public Cid Cid { get; }

        /// <summary>
        /// Gets the <see cref="Commit"/> for the creation operation.
        /// </summary>
        public Commit? Commit { get; }

        /// <summary>
        /// Gets the <see cref="ValidationStatus"/>, if any, for the creation operation.
        /// </summary>
        public ValidationStatus? ValidationStatus { get; }

        /// <summary>
        /// Gets a <see cref="StrongReference"/> to the newly created record.
        /// </summary>
        public StrongReference StrongReference { get; }
    }
}
