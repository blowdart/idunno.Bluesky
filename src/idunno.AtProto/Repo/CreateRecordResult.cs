// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo.Models;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// The result of a CreateRecord API call.
    /// </summary>
    public sealed record CreateRecordResult
    {
        /// <summary>
        /// Creates a new instance of <see cref="CreateRecordResult"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> for the newly created record.</param>
        /// <param name="cid">The <see cref="AtProto.Cid"/> for the newly created record.</param>
        /// <param name="commit">The <see cref="Commit"/> the record was created in.</param>
        /// <param name="validationStatus">The validation status of the record.</param>
        public CreateRecordResult(AtUri uri, Cid cid, Commit? commit, string? validationStatus)
        {
            ArgumentNullException.ThrowIfNull("uri");
            ArgumentNullException.ThrowIfNull("cid");

            Uri = uri;
            Cid = cid;
            StrongReference = new(uri, cid);

            Commit = commit;

            // See https://github.com/dotnet/runtime/issues/114307
            if (validationStatus is not null)
            {
                switch (validationStatus.ToUpperInvariant())
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
        /// Creates a new instance of <see cref="CreateRecordResult"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> for the newly created record.</param>
        /// <param name="cid">The <see cref="AtProto.Cid"/> for the newly created record.</param>
        /// <param name="commit">The <see cref="Commit"/> the record was created in.</param>
        /// <param name="validationStatus">The validation status of the record.</param>
        public CreateRecordResult(AtUri uri, Cid cid, Commit? commit, ValidationStatus? validationStatus)
        {
            ArgumentNullException.ThrowIfNull("uri");
            ArgumentNullException.ThrowIfNull("cid");

            Uri = uri;
            Cid = cid;
            StrongReference = new(uri, cid);

            Commit = commit;
            ValidationStatus = validationStatus;

        }

        internal CreateRecordResult(CreateRecordResponse response)
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
