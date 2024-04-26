// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// A class representing a request to create a record.
    /// </summary>
    internal sealed record CreateRecordRequest
    {
        // https://docs.bsky.app/docs/api/com-atproto-repo-create-record

        /// <summary>
        /// Creates a new instance of the <see cref="CreateRecordRequest"/> class.
        /// </summary>
        /// <param name="collection">The record collection the record should be creating in.</param>
        /// <param name="creatorDecentralizedIdentifier">The <see cref="Did"/> of the actor creating the record.</param>
        /// <param name="record">The <see cref="AtProtoRecord"/> to be created.</param>
        public CreateRecordRequest(
            string collection,
            Did creatorDecentralizedIdentifier,
            NewAtProtoRecord record)
        {
            Collection = collection;
            Repo = creatorDecentralizedIdentifier;
            Record = record;
        }

        /// <summary>
        /// Gets or sets the NSID of the record collection to be created.
        /// </summary>
        /// <value>
        /// The NSID of the record collection to be created.
        /// </value>
        [JsonInclude]
        public string? Collection { get; set; }

        /// <summary>
        /// Gets or sets the DID of the repo the <see cref="AtProtoRecord"/> should be created in in.
        /// </summary>
        /// <value>
        /// The DID of the repo the <see cref="AtProtoRecord"/> should be created in.
        /// </value>
        [JsonRequired]
        public Did Repo { get; set; }

        /// <summary>
        /// Gets or sets the optional record key for the <see cref="AtProtoRecord"/>.
        /// </summary>
        /// <value>
        /// The optional record key for the <see cref="AtProtoRecord"/>.
        /// </value>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RecordKey { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether validation should be performed.
        /// </summary>
        /// <value>
        /// Flag indicating whether validation should be performed.
        /// </value>
        /// <remarks>
        /// Can be set to false to skip Lexicon schema validation of record data.
        /// </remarks>
        public bool Validate { get; set; } = true;

        /// <summary>
        /// Gets or sets the optional previous commit <see cref="AtCid"/> to compare and swap with.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AtCid? SwapCID { get; set; }

        /// <summary>
        /// Gets or sets the record to be created.
        /// </summary>
        [JsonRequired]
        public NewAtProtoRecord Record { get; set; }
    }
}
