// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Encapsulates data returned by the
    /// <see cref="AtProtoServer.PutRecord(object, Nsid, Did, Uri, string, HttpClient, System.Text.Json.JsonSerializerOptions?, CancellationToken)">PutRecord</see>
    /// API call.
    /// </summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
    public sealed record PutRecordResponse
    {
        /// <summary>
        /// Creates a new instance of PutRecordResponse.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> for the newly created record.</param>
        /// <param name="cid">The <see cref="AtProto.Cid"/> for the newly created record.</param>
        /// <param name="commit">The <see cref="Commit"/> the record was created in.</param>
        /// <param name="validationStatus">The <see cref="ValidationStatus"/> used during creation.</param>
        [JsonConstructor]
        public PutRecordResponse(AtUri uri, Cid cid, Commit? commit, ValidationStatus? validationStatus)
        {
            ArgumentNullException.ThrowIfNull("uri");
            ArgumentNullException.ThrowIfNull("cid");

            Uri = uri;
            Cid = cid;
            StrongReference = new(uri, cid);

            Commit = commit;
            ValidationStatus = validationStatus;
        }

        /// <summary>
        /// Gets the <see cref="AtUri"/> for the newly created record.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public AtUri Uri { get; init; }

        /// <summary>
        /// Gets the <see cref="Cid"/> for the newly created record.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Cid Cid { get; init; }

        /// <summary>
        /// Gets the <see cref="Commit"/> for the creation operation.
        /// </summary>
        [JsonInclude]
        public Commit? Commit { get; init; }

        /// <summary>
        /// Gets the <see cref="ValidationStatus"/>, if any, for the creation operation.
        /// </summary>
        [JsonInclude]
        public ValidationStatus? ValidationStatus { get; init; }

        /// <summary>
        /// Gets a <see cref="StrongReference"/> to the newly created record.
        /// </summary>
        [JsonIgnore]
        public StrongReference StrongReference { get; }
    }
}
